using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var auctionsDb = sql.AddDatabase("auctionsdb", "AuctionHouse_Auctions");

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

builder.Build().Run();
