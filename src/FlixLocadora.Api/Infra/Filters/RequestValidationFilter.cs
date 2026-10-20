using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Api.Infra.Filters;

/// <summary>
/// EXPLICIT validation (Foundation.API RequestValidationFilter; FluentValidation.AspNetCore auto-validation is
/// deprecated). For each action argument, looks up an IValidator&lt;T&gt; in DI:
///   • a validator exists and fails → 400 with the envelope and each rule's typed code and field;
///   • no validator → nothing to validate.
/// Rules without an explicit code (WithCode) come out as VALIDATION_FAILED.
/// </summary>
public sealed class RequestValidationFilter(IServiceProvider provider, IRequestContext context) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        foreach (var argument in ctx.ActionArguments.Values.Where(a => a is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (provider.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), ctx.HttpContext.RequestAborted);
            if (result.IsValid)
                continue;

            ctx.Result = new BadRequestObjectResult(new ApiResponse
            {
                Status = ResponseStatus.Fail,
                RequestId = context.RequestId,
                Errors = result.Errors.Select(failure => new Error
                {
                    Code = IsTypedCode(failure.ErrorCode) ? failure.ErrorCode : ErrorCodes.ValidationFailed.Code,
                    Message = failure.ErrorMessage,
                    Field = FieldPath.ToJson(failure.PropertyName),
                    Kind = ErrorKind.Validation
                }).ToList()
            });
            return;
        }

        await next();
    }

    // FluentValidation fills ErrorCode with the validator name ("NotEmptyValidator") when no code was given.
    private static bool IsTypedCode(string? code)
        => !string.IsNullOrWhiteSpace(code) && !code.EndsWith("Validator", StringComparison.Ordinal);
}
