namespace Auctions.Domain.Models;

public static class AuctionStatuses
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Settled = "Settled";
    public const string Unsold = "Unsold";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Draft, Scheduled, Active, Closed, Settled, Unsold, Cancelled
    };
}
