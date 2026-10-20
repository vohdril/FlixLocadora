using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Tests.Infra;

public class ResponseTreatmentTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.Unauthorized, 401)]
    [InlineData(ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.BusinessRule, 422)]
    [InlineData(ErrorKind.Internal, 500)]
    public void Status_comes_from_the_error_kind(ErrorKind kind, int expected)
        => Assert.Equal(expected, ResponseTreatment.StatusFor(Responses.Fail(new ErrorCode("ANY_CODE", "Any message.", kind))));

    [Fact]
    public void Success_uses_the_status_chosen_by_the_controller()
        => Assert.Equal(201, ResponseTreatment.StatusFor(Responses.Ok(42), 201));

    [Fact]
    public void Envelope_and_status_always_agree()
    {
        var result = ResponseTreatment.General(Responses.NotFound());

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(ResponseStatus.Fail, Assert.IsType<ApiResponse>(result.Value).Status);
    }
}
