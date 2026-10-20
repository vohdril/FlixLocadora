using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Business.Common;

/// <summary>The CRUD contract consumed by Controllers/CrudController. Every operation answers an envelope.</summary>
public interface ICrudService<TKey, TQuery, TCreate, TUpdate, TResponse> : IService
    where TKey : notnull
    where TQuery : ListQuery
{
    Task<ApiResponse<PagedResult<TResponse>>> ListAsync(TQuery query, CancellationToken ct);

    Task<ApiResponse<TResponse>> GetAsync(TKey id, CancellationToken ct);

    Task<ApiResponse<TResponse>> CreateAsync(TCreate request, CancellationToken ct);

    Task<ApiResponse<TResponse>> UpdateAsync(TKey id, TUpdate request, CancellationToken ct);

    Task<ApiResponse> DeleteAsync(TKey id, CancellationToken ct);
}

/// <summary>
/// The base service of a CRUD (template method). It does the repetitive part — find, convert, save, answer —
/// and exposes HOOKS for what is specific: Includes, Filter, Order and the Check* business rules.
/// What it fixes from the base projects: Update APPLIES the request onto the tracked entity (it no longer
/// builds a new one that overwrote CreatedAt), filters run in SQL (not in memory), failures are envelopes.
/// </summary>
public abstract class CrudService<TEntity, TKey, TQuery, TCreate, TUpdate, TResponse>(
    IRepository<TEntity> repository,
    IUnitOfWork unitOfWork,
    IEntityConverter<TEntity, TCreate, TUpdate, TResponse> converter)
    : ICrudService<TKey, TQuery, TCreate, TUpdate, TResponse>
    where TEntity : class, IEntity
    where TKey : notnull
    where TQuery : ListQuery
{
    protected static readonly Task<ApiResponse?> NoFailure = Task.FromResult<ApiResponse?>(null);

    protected IRepository<TEntity> Repository { get; } = repository;

    protected IUnitOfWork UnitOfWork { get; } = unitOfWork;

    protected IEntityConverter<TEntity, TCreate, TUpdate, TResponse> Converter { get; } = converter;

    public virtual async Task<ApiResponse<PagedResult<TResponse>>> ListAsync(TQuery query, CancellationToken ct)
    {
        var filtered = Filter(Includes(Repository.Query()), query);
        var total = await filtered.CountAsync(ct);
        var page = await Order(filtered)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Responses.Ok(new PagedResult<TResponse>(page.Select(Converter.ToResponse).ToList(), query.Page, query.PageSize, total));
    }

    public virtual async Task<ApiResponse<TResponse>> GetAsync(TKey id, CancellationToken ct)
    {
        var entity = await Repository.WhereKey(Includes(Repository.Query()), id).FirstOrDefaultAsync(ct);
        return entity is null ? Responses.NotFound<TResponse>() : Responses.Ok(Converter.ToResponse(entity));
    }

    public virtual async Task<ApiResponse<TResponse>> CreateAsync(TCreate request, CancellationToken ct)
    {
        var entity = Converter.ToEntity(request);
        OnCreating(request, entity);
        if (await CheckCreateAsync(request, entity, ct) is { } failure)
            return Responses.Fail<TResponse>(failure);

        Repository.Add(entity);
        await UnitOfWork.SaveChangesAsync(ct);

        // Re-read with the Includes: the response may need navigations (e.g. the customer name of an order).
        return await GetAsync(Repository.KeyOf<TKey>(entity), ct);
    }

    public virtual async Task<ApiResponse<TResponse>> UpdateAsync(TKey id, TUpdate request, CancellationToken ct)
    {
        var entity = await FindTrackedAsync(id, ct);
        if (entity is null)
            return Responses.NotFound<TResponse>();

        Converter.Apply(request, entity);
        OnUpdating(request, entity);
        if (await CheckUpdateAsync(request, entity, ct) is { } failure)
            return Responses.Fail<TResponse>(failure);

        await UnitOfWork.SaveChangesAsync(ct);
        return Responses.Ok(Converter.ToResponse(entity));
    }

    public virtual async Task<ApiResponse> DeleteAsync(TKey id, CancellationToken ct)
    {
        var entity = await FindTrackedAsync(id, ct);
        if (entity is null)
            return Responses.NotFound();

        if (await CheckDeleteAsync(entity, ct) is { } failure)
            return failure;

        Repository.Remove(entity);
        await UnitOfWork.SaveChangesAsync(ct);
        return Responses.Success();
    }

    /// <summary>Loads the entity with its Includes, tracked (it is about to change).</summary>
    protected Task<TEntity?> FindTrackedAsync(TKey id, CancellationToken ct)
        => Repository.WhereKey(Includes(Repository.QueryTracked()), id).FirstOrDefaultAsync(ct);

    /// <summary>Navigations loaded with the entity (e.g. query.Include(o => o.Items)).</summary>
    protected virtual IQueryable<TEntity> Includes(IQueryable<TEntity> query) => query;

    /// <summary>The filters of the list query (search, flags, dates) — translated to SQL.</summary>
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, TQuery parameters) => query;

    /// <summary>Order of the pages; by primary key unless overridden.</summary>
    protected virtual IOrderedQueryable<TEntity> Order(IQueryable<TEntity> query) => Repository.OrderByKey(query);

    /// <summary>Completes the new entity before the rules run (defaults that depend on time or context).</summary>
    protected virtual void OnCreating(TCreate request, TEntity entity)
    {
    }

    /// <summary>Completes the updated entity before the rules run (e.g. UpdatedAt, expected version).</summary>
    protected virtual void OnUpdating(TUpdate request, TEntity entity)
    {
    }

    /// <summary>Business rules that need the database, before an insert (null = no failure).</summary>
    protected virtual Task<ApiResponse?> CheckCreateAsync(TCreate request, TEntity entity, CancellationToken ct) => NoFailure;

    /// <summary>Business rules after the request was applied onto the tracked entity (null = no failure).</summary>
    protected virtual Task<ApiResponse?> CheckUpdateAsync(TUpdate request, TEntity entity, CancellationToken ct) => NoFailure;

    /// <summary>Business rules before a delete (null = no failure).</summary>
    protected virtual Task<ApiResponse?> CheckDeleteAsync(TEntity entity, CancellationToken ct) => NoFailure;
}

public static class UnitOfWorkExtensions
{
    /// <summary>Runs the work in a transaction and commits only when it answers a success envelope.</summary>
    public static Task<TResponse> InTransactionAsync<TResponse>(this IUnitOfWork unitOfWork, Func<CancellationToken, Task<TResponse>> work, CancellationToken ct)
        where TResponse : ApiResponse
        => unitOfWork.InTransactionAsync(work, response => response.Status != ResponseStatus.Fail, ct);
}
