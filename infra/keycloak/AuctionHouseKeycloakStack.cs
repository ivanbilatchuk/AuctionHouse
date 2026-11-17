using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.Inputs;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace AuctionHouse.Keycloak;

// Лаба 9, Частина 3: усе в Keycloak описано кодом (рев'ю, версіонування, відтворюваність `pulumi up` на будь-якому стенді):
//
//   Realm AuctionHouse (RegistrationAllowed, ResetPasswordAllowed, RememberMe, теми)
//   ├─ realm-ролі admin / manager / user / guest + composite service-reader (для service accounts)
//   ├─ Resource Servers: auctions-api, catalog-api, payments-api, community-api
//   │    кожен: scopes (auctions:read ...), ресурс, client-роль service-reader, role-політики, scope-permission на кожен scope
//   ├─ Clients: auctionhouse-postman (Authorization Code, confidential), auctionhouse-swagger (public + PKCE),
//   │           auctionhouse-aggregator (Client Credentials)
//   └─ Users: по одному тестовому користувачу на роль (email підтверджено, тимчасовий пароль)
//
// Keycloak лише видає дозволи: RPT (grant urn:ietf:params:oauth:grant-type:uma-ticket, audience=<resource server>)
// містить authorization.permissions[].scopes. РІШЕННЯ «пропустити / 403» приймає мікросервіс локально — [RequirePermission].
public sealed class AuctionHouseKeycloakStack : Stack
{
    public AuctionHouseKeycloakStack()
    {
        var config = new Pulumi.Config();
        var keycloakConfig = new Pulumi.Config("keycloak");
        var keycloakUrl = (keycloakConfig.Get("url") ?? "http://localhost:8080").TrimEnd('/');
        var realmName = config.Get("realmName") ?? "AuctionHouse";
        var temporaryPasswords = config.GetBoolean("temporaryPasswords") ?? true;
        var testUserPassword = config.GetSecret("testUserPassword") ?? Output.CreateSecret("ChangeMe#2026");
        var swaggerOrigins = (config.Get("swaggerOrigins") ?? "http://localhost:5180,http://localhost:5280,http://localhost:5380,http://localhost:5580")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // ---------------- Realm ----------------
        var realm = new Realm("realm", new RealmArgs
        {
            RealmName = realmName,
            DisplayName = "AuctionHouse",
            Enabled = true,
            RegistrationAllowed = true,
            ResetPasswordAllowed = true,
            RememberMe = true,
            VerifyEmail = true,
            LoginWithEmailAllowed = true,
            DuplicateEmailsAllowed = false,
            LoginTheme = "keycloak",
            AccountTheme = "keycloak.v3",
            AdminTheme = "keycloak.v2",
            AccessTokenLifespan = "15m",
            SslRequired = "external"
        });
        var realmId = realm.Id;

        // ---------------- Realm-ролі ----------------
        var roles = PermissionMatrix.Roles.ToDictionary(role => role, role => new Role($"role-{role}", new RoleArgs
        {
            RealmId = realmId,
            Name = role,
            Description = $"AuctionHouse {role}"
        }));

        // ---------------- Resource Servers, scopes, permissions ----------------
        var serviceReaderClientRoles = new List<Output<string>>();
        var resourceServers = new Dictionary<string, Client>();

        foreach (var spec in PermissionMatrix.ResourceServers)
        {
            var server = ClientFactory.ResourceServer($"rs-{spec.ClientId}", realmId, spec.ClientId, spec.DisplayName);
            resourceServers[spec.ClientId] = server;

            var scopes = spec.Scopes.ToDictionary(scope => scope, scope => new ClientAuthorizationScope($"{spec.ClientId}-scope-{scope}",
                new ClientAuthorizationScopeArgs
                {
                    RealmId = realmId,
                    ResourceServerId = server.ResourceServerId,
                    Name = scope,
                    DisplayName = scope
                }));

            var resource = new ClientAuthorizationResource($"{spec.ClientId}-resource", new ClientAuthorizationResourceArgs
            {
                RealmId = realmId,
                ResourceServerId = server.ResourceServerId,
                Name = spec.ResourceName,
                DisplayName = spec.DisplayName,
                Scopes = spec.Scopes,
                Uris = { $"/api/{spec.ResourceName}/*" }
            }, new CustomResourceOptions { DependsOn = scopes.Values.Cast<Resource>().ToList() });

            // Client-level роль для service accounts (Client Credentials): лише читання цього сервісу.
            var serviceReader = new Role($"{spec.ClientId}-service-reader", new RoleArgs
            {
                RealmId = realmId,
                ClientId = server.Id,
                Name = "service-reader",
                Description = $"Read-only access to {spec.ClientId} for service accounts"
            });
            serviceReaderClientRoles.Add(serviceReader.Id);

            // Role policy на кожну realm-роль + на client-роль service-reader.
            var rolePolicies = PermissionMatrix.Roles.ToDictionary(role => role, role => new ClientRolePolicy($"{spec.ClientId}-policy-{role}",
                new ClientRolePolicyArgs
                {
                    RealmId = realmId,
                    ResourceServerId = server.ResourceServerId,
                    Name = $"role {role}",
                    Type = "role",
                    Logic = "POSITIVE",
                    DecisionStrategy = "UNANIMOUS",
                    Roles = { new ClientRolePolicyRoleArgs { Id = roles[role].Id, Required = true } }
                }));
            var serviceReaderPolicy = new ClientRolePolicy($"{spec.ClientId}-policy-service-reader", new ClientRolePolicyArgs
            {
                RealmId = realmId,
                ResourceServerId = server.ResourceServerId,
                Name = "role service-reader",
                Type = "role",
                Logic = "POSITIVE",
                DecisionStrategy = "UNANIMOUS",
                Roles = { new ClientRolePolicyRoleArgs { Id = serviceReader.Id, Required = true } }
            });

            // Scope-permission на кожен scope: дозволено, якщо спрацювала будь-яка з role-політик (AFFIRMATIVE).
            foreach (var (scopeName, scope) in scopes)
            {
                var policies = PermissionMatrix.Roles
                    .Where(role => spec.RoleScopes[role].Contains(scopeName))
                    .Select(role => rolePolicies[role].Id)
                    .ToList();
                if (scopeName.EndsWith(":read", StringComparison.Ordinal))
                    policies.Add(serviceReaderPolicy.Id);

                _ = new ClientAuthorizationPermission($"{spec.ClientId}-permission-{scopeName}", new ClientAuthorizationPermissionArgs
                {
                    RealmId = realmId,
                    ResourceServerId = server.ResourceServerId,
                    Name = $"permission {scopeName}",
                    Type = "scope",
                    DecisionStrategy = "AFFIRMATIVE",
                    Resources = { resource.Id },
                    Scopes = { scope.Id },
                    Policies = policies
                });
            }
        }

        // Composite realm-роль: групує client-ролі service-reader усіх Resource Server-ів.
        var serviceReaderRealmRole = new Role("role-service-reader", new RoleArgs
        {
            RealmId = realmId,
            Name = "service-reader",
            Description = "Composite: read-only access to all resource servers (service accounts)",
            CompositeRoles = serviceReaderClientRoles
        });

        var audiences = PermissionMatrix.ResourceServers.Select(s => resourceServers[s.ClientId]).ToArray();

        // ---------------- Clients ----------------
        var postman = ClientFactory.AuthorizationCodeConfidential("client-postman", realmId, "auctionhouse-postman",
                "Postman (Authorization Code)", new InputList<string> { "https://oauth.pstmn.io/v1/callback" }, allowDirectAccessGrants: true)
            .WithDefaultScopes("client-postman", realmId)
            .WithAudiences("client-postman", realmId, audiences)
            .WithRealmRolesClaim("client-postman", realmId);

        var swagger = ClientFactory.AuthorizationCodePublic("client-swagger", realmId, "auctionhouse-swagger",
                "Swagger UI (public, PKCE)",
                new InputList<string> { swaggerOrigins.Select(origin => $"{origin}/swagger/oauth2-redirect.html").ToArray() },
                new InputList<string> { swaggerOrigins })
            .WithDefaultScopes("client-swagger", realmId)
            .WithAudiences("client-swagger", realmId, audiences)
            .WithRealmRolesClaim("client-swagger", realmId);

        var aggregator = ClientFactory.ClientCredentials("client-aggregator", realmId, "auctionhouse-aggregator",
                "Platform.Aggregator (Client Credentials)")
            .WithDefaultScopes("client-aggregator", realmId)
            .WithAudiences("client-aggregator", realmId, [resourceServers["auctions-api"], resourceServers["catalog-api"], resourceServers["community-api"]]);

        _ = new ClientServiceAccountRealmRole("aggregator-service-reader", new ClientServiceAccountRealmRoleArgs
        {
            RealmId = realmId,
            ServiceAccountUserId = aggregator.ServiceAccountUserId,
            Role = serviceReaderRealmRole.Name
        });

        // ---------------- Тестові користувачі (по одному на роль) ----------------
        var testUsers = new (string UserName, string FirstName, string LastName, string Role)[]
        {
            ("anna.admin", "Anna", "Admin", PermissionMatrix.Admin),
            ("max.manager", "Max", "Manager", PermissionMatrix.Manager),
            ("ulyana.user", "Ulyana", "User", PermissionMatrix.User),
            ("gena.guest", "Gena", "Guest", PermissionMatrix.Guest)
        };
        foreach (var (userName, firstName, lastName, role) in testUsers)
        {
            var user = new User($"user-{userName}", new UserArgs
            {
                RealmId = realmId,
                Username = userName,
                Enabled = true,
                Email = $"{userName}@auctionhouse.local",
                EmailVerified = true,
                FirstName = firstName,
                LastName = lastName,
                InitialPassword = new UserInitialPasswordArgs { Value = testUserPassword, Temporary = temporaryPasswords }
            });

            _ = new UserRoles($"user-{userName}-roles", new UserRolesArgs
            {
                RealmId = realmId,
                UserId = user.Id,
                RoleIds = { roles[role].Id },
                Exhaustive = false
            });
        }

        // ---------------- Outputs ----------------
        RealmUrl = Output.Format($"{keycloakUrl}/realms/{realm.RealmName}");
        AdminConsoleUrl = Output.Format($"{keycloakUrl}/admin/master/console/#/{realm.RealmName}");
        TokenEndpoint = Output.Format($"{keycloakUrl}/realms/{realm.RealmName}/protocol/openid-connect/token");
        ClientIds = Output.Create(new Dictionary<string, string>
        {
            ["postman"] = "auctionhouse-postman",
            ["swagger"] = "auctionhouse-swagger",
            ["aggregator"] = "auctionhouse-aggregator"
        });
        ResourceServerIds = Output.Create(PermissionMatrix.ResourceServers.Select(s => s.ClientId).ToArray());
        ClientSecrets = Output.CreateSecret(Output.Tuple(postman.ClientSecret, aggregator.ClientSecret)
            .Apply(secrets => new Dictionary<string, string>
            {
                ["postman"] = secrets.Item1,
                ["aggregator"] = secrets.Item2
            }));
        TestUsers = Output.Create(testUsers.ToDictionary(u => u.UserName, u => u.Role));
    }

    [Output] public Output<string> RealmUrl { get; private set; } = null!;
    [Output] public Output<string> AdminConsoleUrl { get; private set; } = null!;
    [Output] public Output<string> TokenEndpoint { get; private set; } = null!;
    [Output] public Output<Dictionary<string, string>> ClientIds { get; private set; } = null!;
    [Output] public Output<string[]> ResourceServerIds { get; private set; } = null!;
    [Output] public Output<Dictionary<string, string>> ClientSecrets { get; private set; } = null!;
    [Output] public Output<Dictionary<string, string>> TestUsers { get; private set; } = null!;
}
