using System.Data;
using BitRoute.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BitRouteDbContext _dbContext;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(BitRouteDbContext dbContext, ILogger<UnitOfWork> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteSerializableAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 5;
        var delay = TimeSpan.FromMilliseconds(50);

        for (int retry = 0; retry < maxRetries; retry++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex) when (IsSerializationFailure(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning("Serialization conflict detected (40001) in Unit of Work on attempt {Retry}. Retrying...", retry + 1);

                if (retry == maxRetries - 1) throw;
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // exponential backoff
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        throw new InvalidOperationException("Failed to complete Serializable transaction after maximum retries.");
    }

    private static bool IsSerializationFailure(Exception ex)
    {
        if (ex is DbUpdateException dbUpdateEx && dbUpdateEx.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState == "40001";
        }
        if (ex is PostgresException postgresEx)
        {
            return postgresEx.SqlState == "40001";
        }
        return false;
    }
}
