using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Loyalty.Api.Modules.Shared;

public record PageInfo(int TotalCount, int Page, int PageSize, int TotalPages);

public record PageResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CursorPageInfo(string? EndCursor, bool HasNextPage);

public record TimestampCursor(DateTimeOffset Timestamp, Guid Id);

public record CursorPageResult<T>(IReadOnlyList<T> Items, string? EndCursor, bool HasNextPage);

public static class CursorPaging
{
    public static string EncodeTimestampCursor(DateTimeOffset timestamp, Guid id)
    {
        var json = JsonSerializer.Serialize(new TimestampCursor(timestamp, id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static TimestampCursor? DecodeTimestampCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<TimestampCursor>(json);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid cursor.", ex);
        }
    }
}

public static class PagingExtensions
{
    public static async Task<PageResult<T>> ToPageResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize, 1, 200);
        var safePage = Math.Max(page, 1);

        var totalCount = await query.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)size);
        if (totalPages > 0 && safePage > totalPages)
        {
            safePage = totalPages;
        }

        var items = await query
            .Skip((safePage - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PageResult<T>(items, totalCount, safePage, size, totalPages);
    }
}
