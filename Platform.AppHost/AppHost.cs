using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var auctionsDb = sql.AddDatabase("auctionsdb", "AuctionHouse_Auctions");
var catalogDb = sql.AddDatabase("catalogdb", "AuctionHouse_Catalog");

var scriptsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "db", "p1"));
builder.Eventing.Subscribe<ResourceReadyEvent>(auctionsDb.Resource, async (@event, cancellationToken) =>
{
    var connectionString = await auctionsDb.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken)
        ?? throw new InvalidOperationException("Connection string of 'auctionsdb' is not available.");

    await DatabaseInitializer.ApplyAsync(connectionString, scriptsDirectory, cancellationToken);
});

builder.AddProject<Projects.Auctions_Api>("auctions-api")
    .WithReference(auctionsDb)
    .WaitFor(auctionsDb)
    .WithHttpHealthCheck("/health");

// Catalog: схему створюють EF Core-міграції (MigrateAsync під час старту API), скрипти не потрібні.
builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

// Community: MongoDB; індекси й сідінг створює сам сервіс під час старту.
var mongo = builder.AddMongoDB("mongo");
var communityDb = mongo.AddDatabase("communitydb", "AuctionHouse_Community");

builder.AddProject<Projects.Community_WebApi>("community-api")
    .WithReference(communityDb)
    .WaitFor(communityDb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
