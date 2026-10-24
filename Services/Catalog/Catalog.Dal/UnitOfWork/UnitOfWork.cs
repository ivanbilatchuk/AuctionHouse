using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Uow;

// Єдина точка збереження: усі зміни репозиторіїв потрапляють у БД одним SaveChangesAsync (одна транзакція).
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Items = new ItemRepository(context);
        Categories = new CategoryRepository(context);
        ItemTags = new ItemTagRepository(context);
        Tags = new GenericRepository<Tag>(context);
    }

    public IItemRepository Items { get; }

    public ICategoryRepository Categories { get; }

    public IItemTagRepository ItemTags { get; }

    public IGenericRepository<Tag> Tags { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The record was modified by another request. Reload it and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A record with the same unique value already exists.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            throw new ConflictException("The operation violates a database constraint (foreign key or check).", ex);
        }
    }
}
