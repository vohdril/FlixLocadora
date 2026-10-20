using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Entities;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Business.Rentals;

public interface IRentalService : IService
{
    Task<ApiResponse<PagedResult<RentalResponse>>> ListAsync(RentalListQuery query, CancellationToken ct);

    Task<ApiResponse<PagedResult<RentalResponse>>> HistoryAsync(int customerId, RentalListQuery query, CancellationToken ct);

    Task<ApiResponse<RentalResponse>> GetAsync(int id, CancellationToken ct);

    Task<ApiResponse<RentalResponse>> RentAsync(CreateRentalRequest request, CancellationToken ct);

    Task<ApiResponse<RentalResponse>> ExtendAsync(int id, UpdateRentalRequest request, CancellationToken ct);

    Task<ApiResponse<RentalResponse>> ReturnAsync(int id, CancellationToken ct);

    Task<ApiResponse> CancelAsync(int id, CancellationToken ct);
}

/// <summary>
/// Rentals are business operations, not a plain CRUD (crudgen.json skips them). Every change is decided by the
/// database, never by "read, check, write" in memory:
///   • the last copy — ONE conditional UPDATE reserves it (MovieRepository.TryReserveCopyAsync), in the same
///     transaction as the INSERT of the rental: whoever loses the race gets MOVIE_UNAVAILABLE, never a negative stock;
///   • the limit per customer — the customer row is locked (FindForUpdateAsync), so two rentals of the same
///     customer run one after the other and see each other; other customers are not blocked;
///   • return, extension and cancellation — the rental row is locked: a double-click waits, then gets
///     RENTAL_ALREADY_RETURNED instead of putting the copy back twice.
/// </summary>
public sealed class RentalService(
    IRepository<Rental> rentals,
    IRepository<Customer> customers,
    IMovieRepository movies,
    IUnitOfWork unitOfWork,
    IOptions<RentalOptions> options,
    TimeProvider clock) : IRentalService
{
    private RentalOptions Rules => options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<ApiResponse<PagedResult<RentalResponse>>> ListAsync(RentalListQuery query, CancellationToken ct)
        => PageAsync(query, query.CustomerId, ct);

    public async Task<ApiResponse<PagedResult<RentalResponse>>> HistoryAsync(int customerId, RentalListQuery query, CancellationToken ct)
        => await customers.Query().AnyAsync(c => c.Id == customerId, ct)
            ? await PageAsync(query, customerId, ct)
            : Responses.NotFound<PagedResult<RentalResponse>>();

    public async Task<ApiResponse<RentalResponse>> GetAsync(int id, CancellationToken ct)
    {
        var rental = await WithNames(rentals.Query()).FirstOrDefaultAsync(r => r.Id == id, ct);
        return rental is null ? Responses.NotFound<RentalResponse>() : Responses.Ok(ToResponse(rental, Now));
    }

    public async Task<ApiResponse<RentalResponse>> RentAsync(CreateRentalRequest request, CancellationToken ct)
    {
        var created = await unitOfWork.InTransactionAsync(async token =>
        {
            var now = Now;

            var customer = await customers.FindForUpdateAsync(request.CustomerId, token);
            if (customer is null)
                return Responses.Fail<int>(RentalErrors.CustomerNotFound, field: "customerId");
            if (!customer.IsActive)
                return Responses.Fail<int>(RentalErrors.CustomerInactive, field: "customerId");

            var open = rentals.Query().Where(r => r.CustomerId == customer.Id && r.ReturnedAt == null);
            if (await open.AnyAsync(r => r.DueAt < now, token))
                return Responses.Fail<int>(RentalErrors.CustomerHasOverdueRentals, field: "customerId");
            if (await open.CountAsync(token) >= Rules.MaxActivePerCustomer)
                return Responses.Fail<int>(RentalErrors.CustomerRentalLimitReached, field: "customerId");

            if (await movies.TryReserveCopyAsync(request.MovieId, token) is not { } dailyRate)
            {
                // Only on the failure path: tell "no copy left" from "no such movie".
                return await movies.Query().AnyAsync(m => m.Id == request.MovieId, token)
                    ? Responses.Fail<int>(RentalErrors.MovieUnavailable, field: "movieId")
                    : Responses.Fail<int>(RentalErrors.MovieNotFound, field: "movieId");
            }

            var rental = RentalPricing.Open(request.MovieId, customer.Id, dailyRate, request.Days, now);
            rentals.Add(rental);
            await unitOfWork.SaveChangesAsync(token);
            return Responses.Ok(rental.Id);
        }, ct);

        return created.Status == ResponseStatus.Fail ? Responses.Fail<RentalResponse>(created) : await GetAsync(created.Data, ct);
    }

    public Task<ApiResponse<RentalResponse>> ExtendAsync(int id, UpdateRentalRequest request, CancellationToken ct)
        => ChangeOpenRentalAsync(id, async (rental, token) =>
        {
            if (RentalPricing.StatusOf(rental, Now) == RentalStatus.Overdue)
                return Responses.Fail(RentalErrors.Overdue);

            var dueAt = RentalPricing.Stored(request.DueAt);
            if (dueAt <= rental.DueAt)
                return Responses.Fail(RentalErrors.DueDateNotLater, field: "dueAt");

            var limit = rental.RentedAt.AddDays(Rules.MaxDays);
            if (dueAt > limit)
                return Responses.Fail(RentalErrors.MaxDaysExceeded, field: "dueAt",
                    message: $"The rental period exceeds the maximum of {Rules.MaxDays} days: dueAt must be at most {limit.ToString("s", CultureInfo.InvariantCulture)}Z.");

            rental.DueAt = dueAt;
            await unitOfWork.SaveChangesAsync(token);
            return Responses.Success();
        }, ct);

    public Task<ApiResponse<RentalResponse>> ReturnAsync(int id, CancellationToken ct)
        => ChangeOpenRentalAsync(id, async (rental, token) =>
        {
            RentalPricing.Close(rental, Now, Rules.LateFeeMultiplier);
            await movies.ReleaseCopyAsync(rental.MovieId, token);
            await unitOfWork.SaveChangesAsync(token);
            return Responses.Success();
        }, ct);

    public Task<ApiResponse> CancelAsync(int id, CancellationToken ct)
        => unitOfWork.InTransactionAsync(async token =>
        {
            var rental = await rentals.FindForUpdateAsync(id, token);
            if (rental is null)
                return Responses.NotFound();
            if (rental.ReturnedAt is not null)
                return Responses.Fail(RentalErrors.AlreadyReturned);
            if (Now - rental.RentedAt > TimeSpan.FromMinutes(Rules.CancelWindowMinutes))
                return Responses.Fail(RentalErrors.CancelWindowExpired);

            await movies.ReleaseCopyAsync(rental.MovieId, token);
            rentals.Remove(rental);
            await unitOfWork.SaveChangesAsync(token);
            return Responses.Success();
        }, ct);

    /// <summary>Locks an OPEN rental, applies <paramref name="change"/> in the same transaction and answers the updated rental.</summary>
    private async Task<ApiResponse<RentalResponse>> ChangeOpenRentalAsync(int id, Func<Rental, CancellationToken, Task<ApiResponse>> change, CancellationToken ct)
    {
        var result = await unitOfWork.InTransactionAsync(async token =>
        {
            var rental = await rentals.FindForUpdateAsync(id, token);
            if (rental is null)
                return Responses.NotFound();
            if (rental.ReturnedAt is not null)
                return Responses.Fail(RentalErrors.AlreadyReturned);

            return await change(rental, token);
        }, ct);

        return result.Status == ResponseStatus.Fail ? Responses.Fail<RentalResponse>(result) : await GetAsync(id, ct);
    }

    private async Task<ApiResponse<PagedResult<RentalResponse>>> PageAsync(RentalListQuery query, int? customerId, CancellationToken ct)
    {
        var now = Now;
        var filtered = WithNames(rentals.Query());

        if (customerId is not null)
            filtered = filtered.Where(r => r.CustomerId == customerId);

        if (query.MovieId is not null)
            filtered = filtered.Where(r => r.MovieId == query.MovieId);

        filtered = query.Status switch
        {
            RentalStatus.Active => filtered.Where(r => r.ReturnedAt == null && r.DueAt >= now),
            RentalStatus.Overdue => filtered.Where(r => r.ReturnedAt == null && r.DueAt < now),
            RentalStatus.Returned => filtered.Where(r => r.ReturnedAt != null),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(r => r.Movie.Title.Contains(search) || r.Customer.Name.Contains(search));
        }

        var total = await filtered.CountAsync(ct);
        var page = await filtered
            .OrderByDescending(r => r.RentedAt).ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return Responses.Ok(new PagedResult<RentalResponse>(page.Select(r => ToResponse(r, now)).ToList(), query.Page, query.PageSize, total));
    }

    private static IQueryable<Rental> WithNames(IQueryable<Rental> query) => query.Include(r => r.Movie).Include(r => r.Customer);

    private static RentalResponse ToResponse(Rental r, DateTime now) => new(
        r.Id, r.MovieId, r.Movie.Title, r.CustomerId, r.Customer.Name, r.RentedAt, r.DueAt, r.ReturnedAt,
        r.DailyRate, r.LateFee, r.TotalAmount, RentalPricing.StatusOf(r, now));
}
