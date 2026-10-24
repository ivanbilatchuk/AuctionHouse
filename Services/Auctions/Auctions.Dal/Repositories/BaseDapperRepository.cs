using System.Data;
using Auctions.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Auctions.Dal.Repositories;

public abstract class BaseDapperRepository<T> : IGenericRepository<T> where T : class
{
    protected const string AuditActor = "Auctions.Api";

    protected BaseDapperRepository(IDbSession session)
    {
        Session = session;
    }

    protected IDbSession Session { get; }

    protected abstract string SelectByIdSql { get; }

    protected abstract string SelectAllSql { get; }

    protected abstract string SoftDeleteSql { get; }

    public abstract Task<long> AddAsync(T entity, CancellationToken cancellationToken = default);

    public abstract Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    public virtual Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<T>(SelectByIdSql, new { Id = id }, cancellationToken: cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await QueryAsync<T>(SelectAllSql, cancellationToken: cancellationToken);

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        await ExecuteAsync(SoftDeleteSql, new { Id = id, Actor = AuditActor }, cancellationToken: cancellationToken) > 0;

    protected async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await Session.GetOpenConnectionAsync(cancellationToken);
            var command = CreateCommand(sql, parameters, commandType, cancellationToken);
            var rows = await connection.QueryAsync<TRow>(command);
            return rows.AsList();
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    protected async Task<IReadOnlyList<TResult>> QueryAsync<TFirst, TSecond, TResult>(
        string sql,
        Func<TFirst, TSecond, TResult> map,
        string splitOn,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await Session.GetOpenConnectionAsync(cancellationToken);
            var command = CreateCommand(sql, parameters, CommandType.Text, cancellationToken);
            var rows = await connection.QueryAsync(command, map, splitOn);
            return rows.AsList();
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    protected async Task<TRow?> QuerySingleOrDefaultAsync<TRow>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await Session.GetOpenConnectionAsync(cancellationToken);
            var command = CreateCommand(sql, parameters, commandType, cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<TRow>(command);
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    protected async Task<TValue> ExecuteScalarAsync<TValue>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await Session.GetOpenConnectionAsync(cancellationToken);
            var command = CreateCommand(sql, parameters, CommandType.Text, cancellationToken);
            return (await connection.ExecuteScalarAsync<TValue>(command))!;
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    protected async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await Session.GetOpenConnectionAsync(cancellationToken);
            var command = CreateCommand(sql, parameters, commandType, cancellationToken);
            return await connection.ExecuteAsync(command);
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    private CommandDefinition CreateCommand(
        string sql,
        object? parameters,
        CommandType commandType,
        CancellationToken cancellationToken) =>
        new(sql, parameters, Session.Transaction, Session.CommandTimeoutSeconds, commandType, cancellationToken: cancellationToken);
}
