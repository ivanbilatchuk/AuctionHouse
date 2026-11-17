using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Platform.EventBus.Abstractions;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.RabbitMq;
using Platform.ServiceDefaults.HealthChecks;

namespace Platform.EventBus;

public static class EventBusExtensions
{
    // Ім'я ресурсу RabbitMQ в AppHost (AddRabbitMQ("messaging")) = ім'я рядка підключення ConnectionStrings:messaging.
    public const string ConnectionName = "messaging";

    // Підключення через Aspire.RabbitMQ.Client (RabbitMQ.Client 7.x): IConnection — singleton на застосунок
    // (з автоматичним відновленням з'єднання), health check, логування клієнта.
    // Канали (IChannel) створюють publisher і consumer-и окремо для своїх операцій.
    public static IHostApplicationBuilder AddEventBus(this IHostApplicationBuilder builder, string connectionName = ConnectionName)
    {
        // Вбудовану трасу RabbitMQ.Client вимкнено: контекст передається вручну (Diagnostics/TracePropagation),
        // щоб у trace не було двох паралельних наборів спанів publish/deliver.
        builder.AddRabbitMQClient(connectionName, settings => settings.DisableTracing = true);

        // Health check від Aspire ("RabbitMQ.Client") потрапляє в /health/ready (тег "ready") зі статусом Degraded:
        // без брокера сервіс і далі обслуговує HTTP-запити, лише копії даних тимчасово не синхронізуються.
        builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations.Where(r => r.Name.StartsWith("RabbitMQ.Client", StringComparison.Ordinal)))
            {
                registration.Tags.Add("messaging");
                registration.Tags.Add(HealthTags.Ready);
                registration.FailureStatus = HealthStatus.Degraded;
                registration.Timeout = TimeSpan.FromSeconds(3);
            }
        });

        builder.Services.AddSingleton<EventBusMetrics>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return builder;
    }
}
