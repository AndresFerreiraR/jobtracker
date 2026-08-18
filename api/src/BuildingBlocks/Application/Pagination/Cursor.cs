using System.Text;

namespace JobTracker.BuildingBlocks.Application.Pagination;

public sealed record Cursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{CreatedAt:O}|{Id}"));

    public static Cursor? TryDecode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2) return null;

            if (!DateTimeOffset.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                return null;
            if (!Guid.TryParse(parts[1], out var id))
                return null;

            return new Cursor(ts, id);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
