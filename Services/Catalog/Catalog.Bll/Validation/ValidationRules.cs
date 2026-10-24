namespace Catalog.Bll.Validation;

internal static class ValidationRules
{
    public const string SkuPattern = "^[A-Z0-9-]+$";
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const string CurrencyPattern = "^[A-Z]{3}$";
}
