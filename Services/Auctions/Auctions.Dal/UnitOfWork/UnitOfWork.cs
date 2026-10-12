using System.Data;
using Auctions.Dal.Interfaces;
using Auctions.Dal.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Auctions.Dal.Uow;

public sealed class UnitOfWork : IUnitOfWork, IDbSession
{
    private readonly string _connectionString;
    private readonly ILogger<UnitOfWork> _logger;

    private SqlConnection? _connection;
    private SqlTransaction? _transaction;

    private IAuctionRepository? _auctions;
    private IBidderRepository? _bidders;
    private IBidRepository? _bids;

    public UnitOfWork(string connectionString, ILogger<UnitOfWork> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is not configured.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _logger = logger;
    }

    public int CommandTimeoutSeconds => 30;

    public SqlTransaction? Transaction => _transaction;

    public bool HasActiveTransaction => _transaction is not null;

    public IAuctionRepository Auctions => _auctions ??= new AuctionRepository(this);

    public IBidderRepository Bidders => _bidders ??= new BidderRepository(this);

    public IBidRepository Bids => _bids ??= new BidRepository(this);

    public async ValueTask<SqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= new SqlConnection(_connectionString);

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active in this Unit of Work.");
        }

        var connection = await GetOpenConnectionAsync(cancellationToken);
        _transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolationLevel, cancellationToken);
        _logger.LogDebug("Transaction started with isolation level {IsolationLevel}", isolationLevel);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("There is no active transaction to commit.");
        }

        try
        {
            await _transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed");
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("Transaction rolled back");
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync();
                _logger.LogWarning("Unit of Work disposed with an active transaction: rolled back");
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async ValueTask DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
