using Community.Domain.Entities;
using MongoDB.Bson;

namespace Community.Domain.Repositories;

public interface IExpertOpinionRepository
{
    Task<ExpertOpinion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task<bool> ExistsForExpertAsync(long itemId, Guid expertUserId, CancellationToken ct = default);

    Task AddAsync(ExpertOpinion opinion, CancellationToken ct = default);

    // Зміна застосовується лише до документа з очікуваною версією, інакше ConflictException.
    Task UpdateAsync(ExpertOpinion opinion, int expectedVersion, CancellationToken ct = default);

    Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default);
}
