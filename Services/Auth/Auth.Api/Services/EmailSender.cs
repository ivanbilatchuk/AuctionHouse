using System.Collections.Concurrent;

namespace Auth.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

// Mock email service для розробки: лист не надсилається, а пишеться в лог і в пам'ять (GET /auth/dev/outbox у Development).
// У production замінюється реальним SMTP / SendGrid без змін у контролерах.
public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    private const int Capacity = 50;
    private readonly ConcurrentQueue<SentEmail> _outbox = new();

    public IReadOnlyCollection<SentEmail> Outbox => _outbox.ToArray();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _outbox.Enqueue(new SentEmail(to, subject, body, DateTime.UtcNow));
        while (_outbox.Count > Capacity && _outbox.TryDequeue(out _)) { }

        logger.LogInformation("[DEV EMAIL] To: {To}; Subject: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(string To, string Subject, string Body, DateTime SentAt);
