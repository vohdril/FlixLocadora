using FluentValidation;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Auth;

// The password POLICY is Identity's (Infra/Auth/IdentityAuth.cs); these rules only reject what is not even a request.

public sealed class RegisterRequestValidator : AValidatorBase<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginRequestValidator : AValidatorBase<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class CreateRoleRequestValidator : AValidatorBase<CreateRoleRequest>
{
    public CreateRoleRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(64).Matches("^[A-Za-z][A-Za-z0-9_-]*$");
}
