namespace FlixLocadora.Api.Data;

/// <summary>
/// Saves the changes of every repository of the request at once (they share the scoped AppDbContext).
/// The base projects had "...Trans" variants of each repository method for this; the unit of work replaces them.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, through the execution strategy (so a transient failure
    /// retries the WHOLE unit), and commits only when <paramref name="commit"/> approves the result.
    /// The work must load what it changes: on a retry the change tracker starts empty.
    /// </summary>
    Task<TResult> InTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken);

    /// <summary>Forgets every tracked entity (e.g. before retrying after a concurrency conflict).</summary>
    void ClearChangeTracker();
}
