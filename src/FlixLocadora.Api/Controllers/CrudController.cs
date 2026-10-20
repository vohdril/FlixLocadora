using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Api.Controllers;

/// <summary>
/// The base controller of a CRUD — the "controladora base" of the base projects, modernized: REST routes come from
/// the derived controller's [Route] (no more [controller]/[action] + Order + ResolveConflictingActions), every
/// action answers the envelope through ResponseTreatment, and conversion happens in the service.
/// A derived controller is one line; it can override an action or add new ones.
/// Response schemas come from ActionResult&lt;T&gt;: attributes cannot use type parameters (CS8968/CS0416).
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class CrudController<TKey, TQuery, TCreate, TUpdate, TResponse>(
    ICrudService<TKey, TQuery, TCreate, TUpdate, TResponse> service) : ControllerBase
    where TKey : notnull
    where TQuery : ListQuery
{
    /// <summary>Lists the records, paged (1-based page).</summary>
    [HttpGet]
    public virtual async Task<ActionResult<ApiResponse<PagedResult<TResponse>>>> List([FromQuery] TQuery query, CancellationToken ct)
        => ResponseTreatment.General(await service.ListAsync(query, ct));

    /// <summary>Gets one record by id.</summary>
    [HttpGet("{id}")]
    public virtual async Task<ActionResult<ApiResponse<TResponse>>> Get(TKey id, CancellationToken ct)
        => ResponseTreatment.General(await service.GetAsync(id, ct));

    /// <summary>Creates a record.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public virtual async Task<ActionResult<ApiResponse<TResponse>>> Create(TCreate request, CancellationToken ct)
        => ResponseTreatment.General(await service.CreateAsync(request, ct), StatusCodes.Status201Created);

    /// <summary>Updates a record.</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Write)]
    public virtual async Task<ActionResult<ApiResponse<TResponse>>> Update(TKey id, TUpdate request, CancellationToken ct)
        => ResponseTreatment.General(await service.UpdateAsync(id, request, ct));

    /// <summary>Deletes a record.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Admin)]
    public virtual async Task<ActionResult<ApiResponse>> Delete(TKey id, CancellationToken ct)
        => ResponseTreatment.General(await service.DeleteAsync(id, ct));
}
