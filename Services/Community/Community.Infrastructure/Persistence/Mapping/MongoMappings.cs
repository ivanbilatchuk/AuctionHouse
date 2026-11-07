using Community.Domain.Common;
using Community.Domain.Entities;
using Community.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace Community.Infrastructure.Persistence.Mapping;

// BSON-мапінг доменних типів через BsonClassMap: у домені немає жодного атрибута MongoDB.
// Реєструється один раз при старті (AddInfrastructure).
public static class MongoMappings
{
    private static int _registered;

    // camelCase-імена елементів, enum як рядок, ігнорування невідомих полів.
    public static readonly IConventionPack Conventions = new ConventionPack
    {
        new CamelCaseElementNameConvention(),
        new EnumRepresentationConvention(BsonType.String),
        new IgnoreExtraElementsConvention(true)
    };

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // Для read-моделей (DTO), які драйвер мапить автоматично під час проєкції.
        ConventionRegistry.Register("community-camelCase", Conventions,
            type => type.Namespace?.StartsWith("Community.", StringComparison.Ordinal) == true);

        BsonClassMap.TryRegisterClassMap<BaseEntity>(cm =>
        {
            cm.MapIdMember(e => e.Id).SetIdGenerator(ObjectIdGenerator.Instance);
            cm.MapMember(e => e.CreatedAt);
            cm.MapMember(e => e.UpdatedAt).SetIgnoreIfNull(true);
            cm.MapMember(e => e.Version);
            ApplyConventions(cm);
        });

        // Value Objects незмінні: створюються через конструктор (MapCreator), без setter-ів.
        BsonClassMap.TryRegisterClassMap<Person>(cm =>
        {
            cm.MapMember(p => p.UserId);
            cm.MapMember(p => p.DisplayName);
            cm.MapCreator(p => new Person(p.UserId, p.DisplayName));
            ApplyConventions(cm);
        });

        BsonClassMap.TryRegisterClassMap<Confidence>(cm =>
        {
            cm.MapMember(c => c.Value);
            cm.MapCreator(c => new Confidence(c.Value));
            ApplyConventions(cm);
        });

        BsonClassMap.TryRegisterClassMap<ItemReference>(cm =>
        {
            cm.MapMember(i => i.ItemId);
            cm.MapMember(i => i.Title);
            cm.MapCreator(i => new ItemReference(i.ItemId, i.Title));
            ApplyConventions(cm);
        });

        BsonClassMap.TryRegisterClassMap<ExpertOpinion>(cm =>
        {
            cm.MapMember(o => o.Item);
            cm.MapMember(o => o.CategoryKind);
            cm.MapMember(o => o.Expert);
            cm.MapMember(o => o.Verdict);
            cm.MapMember(o => o.Confidence);
            cm.MapMember(o => o.Notes).SetIgnoreIfNull(true);
            var attributes = cm.MapField("_attributes");
            ApplyConventions(cm);
            attributes.SetElementName("attributes");
        });

        BsonClassMap.TryRegisterClassMap<Discussion>(cm =>
        {
            cm.MapMember(d => d.AuctionId);
            cm.MapMember(d => d.Item);
            cm.MapMember(d => d.Status);
            cm.MapMember(d => d.CommentCount);
            var comments = cm.MapField("_comments");
            ApplyConventions(cm);
            comments.SetElementName("comments");
        });

        BsonClassMap.TryRegisterClassMap<Comment>(cm =>
        {
            cm.MapIdMember(c => c.Id);
            cm.MapMember(c => c.Author);
            cm.MapMember(c => c.Text);
            cm.MapMember(c => c.CreatedAt);
            var replies = cm.MapField("_replies");
            ApplyConventions(cm);
            replies.SetElementName("replies");
        });
    }

    // AutoMap() тут не використовується: він підключив би публічні конструктори сутностей
    // (ImmutableTypeClassMapConvention). Тому до явно замаплених членів застосовуємо ту саму конвенцію.
    private static void ApplyConventions(BsonClassMap classMap) => new ConventionRunner(Conventions).Apply(classMap);
}
