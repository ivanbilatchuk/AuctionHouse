namespace AuctionHouse.Keycloak;

// Матриця з методички (таблиці 6–7), перенесена на домен AuctionHouse:
// orders-api -> auctions-api, products-api -> catalog-api, payments-api -> payments-api; community-api — додатково.
// Ті самі рядки, що й Permissions у Platform.ServiceDefaults/Security/Permissions.cs (атрибути [RequirePermission]).
public static class PermissionMatrix
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string User = "user";
    public const string Guest = "guest";

    public static readonly string[] Roles = [Admin, Manager, User, Guest];

    public sealed record ResourceServerSpec(string ClientId, string ResourceName, string DisplayName, string[] Scopes,
        IReadOnlyDictionary<string, string[]> RoleScopes);

    public static readonly ResourceServerSpec[] ResourceServers =
    [
        new("auctions-api", "auctions", "Auctions API (Проєкт №1, orders-api)",
            ["auctions:read", "auctions:create", "auctions:update", "auctions:delete", "auctions:approve"],
            new Dictionary<string, string[]>
            {
                [Admin] = ["auctions:read", "auctions:create", "auctions:update", "auctions:delete", "auctions:approve"],
                [Manager] = ["auctions:read", "auctions:update", "auctions:approve"],
                [User] = ["auctions:read", "auctions:create"],
                [Guest] = []
            }),
        new("catalog-api", "catalog", "Catalog API (Проєкт №2, products-api)",
            ["catalog:read", "catalog:create", "catalog:update", "catalog:delete", "catalog:publish"],
            new Dictionary<string, string[]>
            {
                [Admin] = ["catalog:read", "catalog:create", "catalog:update", "catalog:delete", "catalog:publish"],
                [Manager] = ["catalog:read", "catalog:update", "catalog:publish"],
                [User] = ["catalog:read"],
                [Guest] = ["catalog:read"]
            }),
        // Платежі — лише admin: свідоме архітектурне рішення (найчутливіший ресурс), а не недогляд.
        new("payments-api", "payments", "Payments API (демо)",
            ["payments:read", "payments:process", "payments:refund"],
            new Dictionary<string, string[]>
            {
                [Admin] = ["payments:read", "payments:process", "payments:refund"],
                [Manager] = [],
                [User] = [],
                [Guest] = []
            }),
        new("community-api", "community", "Community API (Проєкт №3)",
            ["community:read", "community:write"],
            new Dictionary<string, string[]>
            {
                [Admin] = ["community:read", "community:write"],
                [Manager] = ["community:read", "community:write"],
                [User] = ["community:read", "community:write"],
                [Guest] = ["community:read"]
            })
    ];
}
