namespace Platform.ServiceDefaults.Security;

// Лаба 9: дозволи (permissions) платформи — scopes Resource Server-ів Keycloak, ApiScopes IdentityServer
// і назви політик авторизації [RequirePermission(...)] в мікросервісах. Одна назва — у всіх трьох Identity Provider-ах.
//
// Відповідність методичці: orders-api -> auctions-api (Проєкт №1), products-api -> catalog-api (Проєкт №2),
// payments-api -> payments-api (демо-сервіс). community-api (Проєкт №3) — додатковий Resource Server.
public static class Permissions
{
    public static class Auctions
    {
        public const string Read = "auctions:read";
        public const string Create = "auctions:create";
        public const string Update = "auctions:update";
        public const string Delete = "auctions:delete";
        public const string Approve = "auctions:approve";

        public static readonly string[] All = [Read, Create, Update, Delete, Approve];
    }

    public static class Catalog
    {
        public const string Read = "catalog:read";
        public const string Create = "catalog:create";
        public const string Update = "catalog:update";
        public const string Delete = "catalog:delete";
        public const string Publish = "catalog:publish";

        public static readonly string[] All = [Read, Create, Update, Delete, Publish];
    }

    public static class Payments
    {
        public const string Read = "payments:read";
        public const string Process = "payments:process";
        public const string Refund = "payments:refund";

        public static readonly string[] All = [Read, Process, Refund];
    }

    public static class Community
    {
        public const string Read = "community:read";
        public const string Write = "community:write";

        public static readonly string[] All = [Read, Write];
    }

    public static readonly string[] All = [.. Auctions.All, .. Catalog.All, .. Payments.All, .. Community.All];
}

// Матриця «роль -> дозволи» (таблиця 7 методички). Keycloak застосовує її через Permission Policies (infra/keycloak),
// а для токенів Auth Service (ролі User / Admin, Частина 1) мікросервіс застосовує її сам — локально, без звернення до IdP.
// payments-api свідомо дає дозволи ЛИШЕ admin: платежі — найчутливіший ресурс.
public static class RolePermissions
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string User = "user";
    public const string Guest = "guest";

    public static readonly IReadOnlyDictionary<string, string[]> Matrix =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Admin] = Permissions.All,
            [Manager] =
            [
                Permissions.Auctions.Read, Permissions.Auctions.Update, Permissions.Auctions.Approve,
                Permissions.Catalog.Read, Permissions.Catalog.Update, Permissions.Catalog.Publish,
                Permissions.Community.Read, Permissions.Community.Write
            ],
            [User] =
            [
                Permissions.Auctions.Read, Permissions.Auctions.Create,
                Permissions.Catalog.Read,
                Permissions.Community.Read, Permissions.Community.Write
            ],
            [Guest] =
            [
                Permissions.Catalog.Read,
                Permissions.Community.Read
            ]
        };

    public static IEnumerable<string> ForRole(string role) =>
        Matrix.TryGetValue(role, out var permissions) ? permissions : [];
}
