using System.Diagnostics;

namespace Catalog.Bll.Diagnostics;

// Джерело бізнес-спанів каталогу (реєструється в ServiceDefaults через AddSource("AuctionHouse.*")).
public static class CatalogTelemetry
{
    public const string ActivitySourceName = "AuctionHouse.Catalog";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
