using MongoDB.Bson;

namespace Community.Domain.Common;

public abstract class BaseEntity
{
    protected BaseEntity()
    {
        Id = ObjectId.GenerateNewId();
        CreatedAt = DateTime.UtcNow;
        Version = 1;
    }

    public ObjectId Id { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    // Версія документа для оптимістичної конкурентності: зростає при кожній зміні стану.
    public int Version { get; private set; }

    protected void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
