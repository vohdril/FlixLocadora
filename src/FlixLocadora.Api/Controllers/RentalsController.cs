using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Rentals;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Api.Controllers;

/// <summary>Rentals: rent, extend, return, cancel — and the history of a customer.</summary>
[ApiController]
[Route("api/rentals")]
[Produces("application/json")]
[Authorize(Policy = Policies.Read)]
[AllowAnonymous] // TODO(auth go-live): remove this line to enforce the policies
public sealed class RentalsController(IRentalService rentals) : ControllerBase
{
    /// <summary>Lists the rentals (status: Active, Overdue or Returned — derived from the dates).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RentalResponse>>>> List([FromQuery] RentalListQuery query, CancellationToken ct)
        => ResponseTreatment.General(await rentals.ListAsync(query, ct));

    /// <summary>Gets a rental.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RentalResponse>>> Get(int id, CancellationToken ct)
        => ResponseTreatment.General(await rentals.GetAsync(id, ct));

    /// <summary>Rents one copy of a movie (the last copy is decided by the database, atomically).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<RentalResponse>>> Rent(CreateRentalRequest request, CancellationToken ct)
        => ResponseTreatment.General(await rentals.RentAsync(request, ct), StatusCodes.Status201Created);

    /// <summary>Extends an open rental that is not overdue (new due date).</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Write)]
    public async Task<ActionResult<ApiResponse<RentalResponse>>> Extend(int id, UpdateRentalRequest request, CancellationToken ct)
        => ResponseTreatment.General(await rentals.ExtendAsync(id, request, ct));

    /// <summary>Returns the copy: computes the late fee and the total, and puts the copy back in stock.</summary>
    [HttpPost("{id}/return")]
    [Authorize(Policy = Policies.Write)]
    public async Task<ActionResult<ApiResponse<RentalResponse>>> Return(int id, CancellationToken ct)
        => ResponseTreatment.General(await rentals.ReturnAsync(id, ct));

    /// <summary>Cancels a rental registered by mistake, within Rental:CancelWindowMinutes (the copy goes back to stock).</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Write)]
    public async Task<ActionResult<ApiResponse>> Cancel(int id, CancellationToken ct)
        => ResponseTreatment.General(await rentals.CancelAsync(id, ct));

    /// <summary>Rental history of a customer (the customerId of the query string is ignored).</summary>
    [HttpGet("/api/customers/{customerId}/rentals")]
    public async Task<ActionResult<ApiResponse<PagedResult<RentalResponse>>>> History(int customerId, [FromQuery] RentalListQuery query, CancellationToken ct)
        => ResponseTreatment.General(await rentals.HistoryAsync(customerId, query, ct));
}
