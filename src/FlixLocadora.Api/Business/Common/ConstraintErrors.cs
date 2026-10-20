namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// Maps database constraint names to typed errors. The constraint is the REAL guarantee (two simultaneous
/// requests both pass a service pre-check; only one passes the UNIQUE constraint) — this catalog turns the
/// database refusal into the same 409/422 the pre-check would give, instead of a 500.
/// Each feature declares its own catalog; Infra/Errors/DbExceptionHandler reads them all.
/// </summary>
public interface IConstraintErrorCatalog : IService
{
    /// <param name="constraintName">Name of the violated constraint or index (e.g. UQ_Customer_Email).</param>
    /// <param name="statement">INSERT, UPDATE or DELETE when SQL Server says which one (foreign keys), otherwise null.</param>
    /// <param name="error">The typed error of the constraint, when the catalog knows it.</param>
    bool TryGet(string constraintName, string? statement, out Error error);
}

public abstract class ConstraintErrorCatalog : IConstraintErrorCatalog
{
    private readonly Dictionary<(string Constraint, string Statement), (ErrorCode Code, string? Field)> _map = [];

    /// <summary>
    /// Maps a constraint to an error. <paramref name="statement"/> narrows a foreign key to one direction: the same
    /// FK means "the customer has orders" on DELETE but "the customer does not exist" on INSERT.
    /// </summary>
    protected void Map(string constraintName, ErrorCode code, string? field = null, string? statement = null)
        => _map[(constraintName.ToUpperInvariant(), statement?.ToUpperInvariant() ?? "*")] = (code, field);

    public bool TryGet(string constraintName, string? statement, out Error error)
    {
        var constraint = constraintName.ToUpperInvariant();
        if (_map.TryGetValue((constraint, statement?.ToUpperInvariant() ?? "*"), out var entry) || _map.TryGetValue((constraint, "*"), out entry))
        {
            error = entry.Code.ToError(entry.Field);
            return true;
        }

        error = null!;
        return false;
    }
}
