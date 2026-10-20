using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Data.Entities;

namespace FlixLocadora.Api.Data.Repositories;

/// <summary>
/// The stock of copies changes ONLY through atomic, conditional UPDATEs: the database decides, in one statement,
/// whether there is a copy left — "read, check, write" would let two clerks rent the last copy at the same time.
/// ExecuteUpdate runs outside SaveChanges: callers wrap it in IUnitOfWork.InTransactionAsync with the rest.
/// CK_Movie_AvailableCopies (0 ≤ available ≤ total) is the safety net behind these statements.
/// </summary>
public interface IMovieRepository : IRepository<Movie>
{
    /// <summary>Takes one copy of an active movie; null when there is none left (or the movie is inactive/missing).</summary>
    Task<decimal?> TryReserveCopyAsync(int movieId, CancellationToken ct);

    /// <summary>Puts one copy back.</summary>
    Task ReleaseCopyAsync(int movieId, CancellationToken ct);

    /// <summary>Sets the stock keeping the rented copies; false when the new total is below them.</summary>
    Task<bool> TrySetTotalCopiesAsync(int movieId, int totalCopies, CancellationToken ct);
}

public sealed class MovieRepository(AppDbContext db) : Repository<Movie>(db), IMovieRepository
{
    public async Task<decimal?> TryReserveCopyAsync(int movieId, CancellationToken ct)
    {
        var reserved = await Set
            .Where(m => m.Id == movieId && m.IsActive && m.AvailableCopies > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AvailableCopies, m => m.AvailableCopies - 1), ct);

        return reserved == 0
            ? null
            : await Set.Where(m => m.Id == movieId).Select(m => m.DailyRate).SingleAsync(ct);
    }

    public Task ReleaseCopyAsync(int movieId, CancellationToken ct)
        => Set.Where(m => m.Id == movieId && m.AvailableCopies < m.TotalCopies)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AvailableCopies, m => m.AvailableCopies + 1), ct);

    public async Task<bool> TrySetTotalCopiesAsync(int movieId, int totalCopies, CancellationToken ct)
        => await Set
            .Where(m => m.Id == movieId && m.TotalCopies - m.AvailableCopies <= totalCopies)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.AvailableCopies, m => totalCopies - (m.TotalCopies - m.AvailableCopies))
                .SetProperty(m => m.TotalCopies, totalCopies), ct) == 1;
}
