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

// Catalog: схему створюють EF Core-міграції (MigrateAsync під час старту API), скрипти не потрібні.
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api", launchProfileName: "http")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

// Community: MongoDB; індекси й сідінг створює сам сервіс під час старту.
var communityApi = builder.AddProject<Projects.Community_WebApi>("community-api", launchProfileName: "http")
    .WithReference(communityDb)
    .WaitFor(communityDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

// Агрегатор: сервіси -> aggregator (адреси сервісів, у т. ч. _grpc-ендпоінти, — через service discovery).
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(redis).WaitFor(redis)
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    .WithHttpHealthCheck("/health/ready");

// Gateway — єдина точка входу на фіксованому порту http://localhost:5000.
builder.AddProject<Projects.Platform_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    .WithReference(aggregator).WaitFor(aggregator)
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();
