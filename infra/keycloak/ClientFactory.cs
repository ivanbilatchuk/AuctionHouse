using Pulumi;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace AuctionHouse.Keycloak;

// Фабрика clients: однакові налаштування для кожного типу flow в одному місці.
public static class ClientFactory
{
    // Resource Server: confidential client з увімкненою Authorization (Keycloak Authorization Services).
    // Сам не логінить користувачів — він лише «власник» scopes і permissions, які потрапляють у RPT.
    public static Client ResourceServer(string name, Input<string> realmId, string clientId, string displayName) =>
        new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = true,   // обов'язково для Authorization Services
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                DecisionStrategy = "AFFIRMATIVE",
                AllowRemoteResourceManagement = false,
                KeepDefaults = false
            }
        });

    // Authorization Code Flow — confidential (Postman). Direct Access Grants — лише для тестування в розробці (curl / скрипти).
    public static Client AuthorizationCodeConfidential(string name, Input<string> realmId, string clientId, string displayName,
        InputList<string> redirectUris, bool allowDirectAccessGrants) =>
        new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = allowDirectAccessGrants,
            ServiceAccountsEnabled = false,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirectUris
        });

    // Authorization Code Flow — public client з PKCE (Swagger UI): секрету немає, тому без code_challenge запит відхиляється.
    public static Client AuthorizationCodePublic(string name, Input<string> realmId, string clientId, string displayName,
        InputList<string> redirectUris, InputList<string> webOrigins) =>
        new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "PUBLIC",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = false,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirectUris,
            WebOrigins = webOrigins
        });

    // Client Credentials Flow (Aggregator): лише service account, без участі користувача.
    public static Client ClientCredentials(string name, Input<string> realmId, string clientId, string displayName) =>
        new(name, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = true
        });
}
