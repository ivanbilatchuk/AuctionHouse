using Duende.AccessTokenManagement;

namespace Platform.Aggregator.Security;

// Лаба 9: отримує токен Client Credentials під час старту. Перший запит до IdentityServer (створення ключів підпису,
// прогрів EF) може тривати довше за таймаут спроби виклику сервісу (2 с) — без прогріву перший запит користувача
// отримав би 503. Далі токен береться з кешу й оновлюється до закінчення терміну дії. Збій не зупиняє застосунок.
public sealed class ServiceTokenWarmup(IClientCredentialsTokenManager tokens, ILogger<ServiceTokenWarmup> logger) : BackgroundService
{
    // Ім'я клієнта токенів: client "aggregator_service" в IdentityServer (Program.cs, AddClientCredentialsTokenManagement).
    public static readonly ClientCredentialsClientName ClientName = ClientCredentialsClientName.Parse("identityserver.aggregator");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 10 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var result = await tokens.GetAccessTokenAsync(ClientName, ct: stoppingToken);
                if (result.Succeeded)
                {
                    logger.LogInformation("Service access token for {Client} obtained (expires {Expiration:u})",
                        ClientName.ToString(), result.Token.Expiration);
                    return;
                }

                logger.LogWarning("Service access token for {Client} not obtained (attempt {Attempt}): {Error}",
                    ClientName.ToString(), attempt, result.FailedResult.Error);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Service access token for {Client} not obtained (attempt {Attempt})", ClientName.ToString(), attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
