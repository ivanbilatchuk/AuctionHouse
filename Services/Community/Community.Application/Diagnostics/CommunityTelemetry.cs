using System.Diagnostics;

namespace Community.Application.Diagnostics;

// Джерело бізнес-спанів спільноти (реєструється в ServiceDefaults через AddSource("AuctionHouse.*")).
public static class CommunityTelemetry
{
    public const string ActivitySourceName = "AuctionHouse.Community";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
