namespace JobTracker.BuildingBlocks.Application.Pagination;

public sealed record PagedList<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public static PagedList<T> FromKeyset<TRow>(
        IReadOnlyList<TRow> rows,
        int pageSize,
        Func<TRow, Cursor> keyOf,
        Func<TRow, T> map)
    {
        var hasMore = rows.Count > pageSize;
        var page = (hasMore ? rows.Take(pageSize) : rows).Select(map).ToArray();
        var next = hasMore ? keyOf(rows[pageSize - 1]).Encode() : null;
        return new PagedList<T>(page, next);
    }
}
