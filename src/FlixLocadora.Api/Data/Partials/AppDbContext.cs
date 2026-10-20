using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    /// <summary>
    /// Every date-time of this database is UTC (sysutcdatetime(), TimeProvider), but datetime2 has no kind: EF would
    /// read Unspecified and the JSON would go out WITHOUT the "Z" — a client in UTC-3 would show it 3 hours off.
    /// Done here, not in a JSON converter: a custom converter erases "type: string" from the OpenAPI schema.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
