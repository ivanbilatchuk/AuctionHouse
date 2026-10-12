using System.Data;
using Auctions.Dal.Interfaces;

namespace Auctions.Bll.Services;

internal static class UnitOfWorkExtensions
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this IUnitOfWork unitOfWork,
        Func<Task<T>> action,
        CancellationToken cancellationToken,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        await unitOfWork.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            var result = await action();
            await unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
