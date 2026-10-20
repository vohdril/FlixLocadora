using Microsoft.EntityFrameworkCore;

namespace FlixLocadora.Api.Data;

/// <summary>
/// Hand-written half of the generated AppDbContext (Data/Generated/AppDbContext.cs is rewritten by every scaffold).
/// </summary>
public partial class AppDbContext : IUnitOfWork
{
    public async Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken)
    {
        // With EnableRetryOnFailure, EF refuses a user-initiated transaction outside the execution strategy.
        var strategy = Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async ct =>
        {
            if (attempt++ > 0)
                ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);

            if (commit(result))
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await transaction.RollbackAsync(ct);
                ChangeTracker.Clear();
            }

            return result;
        }, cancellationToken);
    }

    public void ClearChangeTracker() => ChangeTracker.Clear();
}
