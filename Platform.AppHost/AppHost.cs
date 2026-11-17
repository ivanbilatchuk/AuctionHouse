using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Пароль SQL Server — секретний параметр: dotnet user-secrets set Parameters:sql-password "<пароль>" (у Platform.AppHost).
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Імена ресурсів БД збігаються з іменами рядків підключення в сервісах (ConnectionStrings:AuctionsDb, ...),
// другий аргумент — фізична назва бази.
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume();
var auctionsDb = sql.AddDatabase("AuctionsDb", "AuctionHouse_Auctions");
var catalogDb = sql.AddDatabase("CatalogDb", "AuctionHouse_Catalog");

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var communityDb = mongo.AddDatabase("CommunityDb", "AuctionHouse_Community");

// Лаба 6: Redis — L2-кеш сервісів і кеш готових карток агрегатора (IDistributedCache).
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Лаба 8: брокер повідомлень RabbitMQ. Облікові дані генерує Aspire, сервіси отримують рядок підключення
// "messaging" через WithReference. WithDataVolume — durable-черги й persistent-повідомлення переживають перезапуск;
// WithManagementPlugin — веб-інтерфейс керування (exchanges, черги, bindings, DLQ) — посилання в Dashboard.
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin();

// ---------------- Лаба 9: Identity Provider-и ----------------
// Ключ підпису токенів Auth Service (HS256): генерується один раз і зберігається в user-secrets AppHost.
// Отримують його Auth Service (підписує) і всі сервіси / Gateway (валідують локально).
var jwtSigningKey = builder.AddParameter("jwt-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false }, secret: true, persist: true);

// Окремий SQL Server для даних безпеки: IdentityDb (Частина 1, Auth Service) і IdentityServerDb (Частина 2) —
// ізольовані від бізнес-баз (sql). Компрометація бізнес-бази не відкриває доступу до облікових записів і ключів.
var identitySql = builder.AddSqlServer("identity-sql", sqlPassword)
    .WithDataVolume();
var identityDb = identitySql.AddDatabase("IdentityDb", "AuctionHouse_Identity");
var identityServerDb = identitySql.AddDatabase("IdentityServerDb", "AuctionHouse_IdentityServer");

// Частина 1: Auth Service (ASP.NET Identity + JWT, http://localhost:5680). Через Gateway: /auth/**, /users/**.
var authApi = builder.AddProject<Projects.Auth_Api>("auth-api", launchProfileName: "http")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Authentication__AuthService__SigningKey", jwtSigningKey)
    .WithHttpHealthCheck("/health/ready");

// Частина 2: Duende IdentityServer (http://localhost:5700) — Authorization Code (Postman, Swagger) і Client Credentials (Aggregator).
var identityServer = builder.AddProject<Projects.Platform_IdentityServer>("identityserver", launchProfileName: "http")
    .WithReference(identityServerDb)
    .WaitFor(identityServerDb)
    .WithHttpHealthCheck("/health/ready");
// Issuer (http://localhost:5700) задано в appsettings.json IdentityServer: посилання ресурсу на власний endpoint
// через WithEnvironment блокує його створення (Aspire чекає на endpoint, який ще не виділено).
var identityServerUrl = identityServer.GetEndpoint("http");

// Частина 3 (траєкторія Б): Keycloak через Aspire-інтеграцію, http://localhost:8080 (Admin Console, логін admin).
// WithDataVolume — realm, клієнти й користувачі (створені `pulumi up` з infra/keycloak) переживають перезапуск.
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password",
    new GenerateParameterDefault { MinLength = 16, Special = false }, secret: true, persist: true);
var keycloak = builder.AddKeycloak("keycloak", 8080, adminPassword: keycloakAdminPassword)
    .WithDataVolume();

// Спільні налаштування автентифікації для мікросервісів, Aggregator-а і Gateway: ключ Auth Service, Authority IdentityServer,
// посилання на Keycloak (AddKeycloakJwtBearer знаходить його через service discovery).
IResourceBuilder<ProjectResource> WithPlatformAuth(IResourceBuilder<ProjectResource> project) => project
    .WithEnvironment("Authentication__AuthService__SigningKey", jwtSigningKey)
    .WithEnvironment("Authentication__IdentityServer__Authority", identityServerUrl)
    .WithReference(keycloak);

var scriptsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "db", "p1"));
builder.Eventing.Subscribe<ResourceReadyEvent>(auctionsDb.Resource, async (@event, cancellationToken) =>
{
    var connectionString = await auctionsDb.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken)
        ?? throw new InvalidOperationException("Connection string of 'AuctionsDb' is not available.");

    await DatabaseInitializer.ApplyAsync(connectionString, scriptsDirectory, cancellationToken);
});

// Лаба 7: WithHttpHealthCheck("/health/ready") — Dashboard показує стан ресурсу (Healthy / Degraded / Unhealthy)
// за readiness-перевірками сервісу (БД, Redis, gRPC-залежності), а WaitFor чекає, поки залежність стане
// ЗДОРОВОЮ, а не лише запуститься. Недоступний Redis дає Degraded — сервіс працює далі (читає з БД).
//
// Сервіси: БД + Redis + RabbitMQ -> сервіс (Auctions і Catalog публікують і споживають події, Community — споживає). Профіль "http": health check і виклики агрегатора йдуть по http,
// без залежності від довіреного dev-сертифіката (на Linux він зазвичай не довірений).
// Ендпоінти сервісів беруться з Kestrel:Endpoints у їхніх appsettings.json:
// "http" (HTTP/1.1, REST + health) і "grpc" (HTTP/2 без TLS) — Aspire створює для кожного свій ресурсний ендпоінт.
var auctionsApi = builder.AddProject<Projects.Auctions_Api>("auctions-api", launchProfileName: "http")
    .WithReference(auctionsDb)
    .WaitFor(auctionsDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
WithPlatformAuth(auctionsApi);

// Catalog: схему створюють EF Core-міграції (MigrateAsync під час старту API), скрипти не потрібні.
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api", launchProfileName: "http")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
WithPlatformAuth(catalogApi);

// Community: MongoDB; індекси й сідінг створює сам сервіс під час старту.
var communityApi = builder.AddProject<Projects.Community_WebApi>("community-api", launchProfileName: "http")
    .WithReference(communityDb)
    .WaitFor(communityDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
WithPlatformAuth(communityApi);

// Лаба 9: демо-сервіс платежів (http://localhost:5580) — перевірка доступу: payments:* має лише admin.
var paymentsApi = builder.AddProject<Projects.Payments_Api>("payments-api", launchProfileName: "http")
    .WithHttpHealthCheck("/health/ready");
WithPlatformAuth(paymentsApi);

// Агрегатор: сервіси -> aggregator (адреси сервісів, у т. ч. _grpc-ендпоінти, — через service discovery).
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(redis).WaitFor(redis)
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    // Лаба 9: токен Client Credentials для викликів сервісів — від IdentityServer.
    .WithReference(identityServer).WaitFor(identityServer)
    .WithHttpHealthCheck("/health/ready");
WithPlatformAuth(aggregator);

// Gateway — єдина точка входу на фіксованому порту http://localhost:5000.
var gateway = builder.AddProject<Projects.Platform_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    .WithReference(aggregator).WaitFor(aggregator)
    .WithReference(authApi).WaitFor(authApi)
    .WithReference(paymentsApi).WaitFor(paymentsApi)
    .WithHttpHealthCheck("/health/ready");
WithPlatformAuth(gateway);

builder.Build().Run();
