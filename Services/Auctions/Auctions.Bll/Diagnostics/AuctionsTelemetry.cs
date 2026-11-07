using System.Diagnostics;

namespace Auctions.Bll.Diagnostics;

// Джерело бізнес-спанів сервісу аукціонів (реєструється в ServiceDefaults через AddSource("AuctionHouse.*")).
public static class AuctionsTelemetry
{
    public const string ActivitySourceName = "AuctionHouse.Auctions";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
