using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace AuctionHouse.Keycloak;

// Спільні налаштування clients, винесені в extension methods: default scopes і protocol mappers.
public static class ClientExtensions
{
    // Default client scopes: профіль, email, ролі (realm_access) — у кожному токені клієнта.
    public static Client WithDefaultScopes(this Client client, string name, Input<string> realmId, params string[] extraScopes)
    {
        _ = new ClientDefaultScopes($"{name}-default-scopes", new ClientDefaultScopesArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            DefaultScopes = new[] { "basic", "profile", "email", "roles", "web-origins", "acr" }.Concat(extraScopes).ToList()
        });
        return client;
    }

    // Audience mapper: додає Resource Server-и в "aud" звичайного access token, щоб мікросервіси прийняли його й без RPT.
    // Аудиторія береться з Output клієнта Resource Server-а: так Pulumi знає залежність і створює mapper лише
    // після того, як клієнт-аудиторія існує (інакше Keycloak: "generic client with name ... does not exist").
    public static Client WithAudiences(this Client client, string name, Input<string> realmId, IEnumerable<Client> audiences)
    {
        foreach (var audience in audiences)
            _ = new AudienceProtocolMapper($"{name}-aud-{audience.GetResourceName()["rs-".Length..]}", new AudienceProtocolMapperArgs
            {
                RealmId = realmId,
                ClientId = client.Id,
                Name = audience.ClientId.Apply(id => $"audience {id}"),
                IncludedClientAudience = audience.ClientId,
                AddToAccessToken = true,
                AddToIdToken = false
            });
        return client;
    }

    // Realm-ролі окремим плоским claim "realm_roles" (крім стандартного realm_access.roles) — зручно для діагностики.
    public static Client WithRealmRolesClaim(this Client client, string name, Input<string> realmId)
    {
        _ = new UserRealmRoleProtocolMapper($"{name}-realm-roles", new UserRealmRoleProtocolMapperArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = "realm roles",
            ClaimName = "realm_roles",
            Multivalued = true,
            AddToAccessToken = true,
            AddToIdToken = false,
            AddToUserinfo = true
        });
        return client;
    }
}
