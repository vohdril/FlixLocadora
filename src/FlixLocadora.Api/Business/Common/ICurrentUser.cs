namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// Who is calling, for the services (e.g. ownership rules). While the controllers are [AllowAnonymous] there is
/// no user: IsAuthenticated is false and the services must not assume one.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
