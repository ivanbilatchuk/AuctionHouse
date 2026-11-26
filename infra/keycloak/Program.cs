using AuctionHouse.Keycloak;

// pulumi preview  — показати, що буде створено (realm, resource servers, scopes, ролі, політики, clients, users)
// pulumi up       — створити / оновити
// pulumi stack output
// pulumi destroy  — прибрати після демонстрації
return await Pulumi.Deployment.RunAsync<AuctionHouseKeycloakStack>();
