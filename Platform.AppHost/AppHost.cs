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

var scriptsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "db", "p1"));
builder.Eventing.Subscribe<ResourceReadyEvent>(auctionsDb.Resource, async (@event, cancellationToken) =>
{
    var connectionString = await auctionsDb.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken)
        ?? throw new InvalidOperationException("Connection string of 'AuctionsDb' is not available.");

    await DatabaseInitializer.ApplyAsync(connectionString, scriptsDirectory, cancellationToken);
});

// Сервіси: БД -> сервіс. Профіль "http": health check і виклики агрегатора йдуть по http,
// без залежності від довіреного dev-сертифіката (на Linux він зазвичай не довірений).
var auctionsApi = builder.AddProject<Projects.Auctions_Api>("auctions-api", launchProfileName: "http")
    .WithReference(auctionsDb)
    .WaitFor(auctionsDb)
    .WithHttpHealthCheck("/health");

// Catalog: схему створюють EF Core-міграції (MigrateAsync під час старту API), скрипти не потрібні.
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api", launchProfileName: "http")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

// Community: MongoDB; індекси й сідінг створює сам сервіс під час старту.
var communityApi = builder.AddProject<Projects.Community_WebApi>("community-api", launchProfileName: "http")
    .WithReference(communityDb)
    .WaitFor(communityDb)
    .WithHttpHealthCheck("/health");

// Агрегатор: сервіси -> aggregator (адреси сервісів — через service discovery).
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    .WithHttpHealthCheck("/health");

// Gateway — єдина точка входу на фіксованому порту http://localhost:5000.
builder.AddProject<Projects.Platform_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(auctionsApi).WaitFor(auctionsApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(communityApi).WaitFor(communityApi)
    .WithReference(aggregator).WaitFor(aggregator)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
