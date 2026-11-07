namespace Community.Infrastructure.Persistence;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "AuctionHouse_Community";

    public bool SeedOnStartup { get; set; } = true;
}
