using Community.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Community.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    public const string ExpertOpinionsCollection = "expertOpinions";
    public const string DiscussionsCollection = "discussions";

    public MongoDbContext(IOptions<MongoDbSettings> options, IMongoClient client)
    {
        Client = client;
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<ExpertOpinion> ExpertOpinions => Database.GetCollection<ExpertOpinion>(ExpertOpinionsCollection);

    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);
}
