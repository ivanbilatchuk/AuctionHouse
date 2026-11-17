using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Payments.Api.Controllers;

/// <summary>
/// Демо-сервіс платежів (Лаба 9) для перевірки permission-based доступу: платежі — найчутливіший ресурс,
/// тому payments:* має ЛИШЕ роль admin (manager / user / guest отримують 403). Дані — в пам'яті (заглушки).
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController(ILogger<PaymentsController> logger) : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, PaymentDto> Store = new();

    /// <summary>Список платежів.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Payments.Read)]
    [ProducesResponseType<IReadOnlyCollection<PaymentDto>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(Store.Values.OrderByDescending(p => p.CreatedAt));

    /// <summary>Провести платіж за аукціоном (заглушка).</summary>
    [HttpPost("process")]
    [RequirePermission(Permissions.Payments.Process)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    public IActionResult Process(ProcessPaymentRequest request)
    {
        var payment = new PaymentDto(Guid.NewGuid(), request.AuctionId, request.Amount, request.Currency.ToUpperInvariant(),
            "Completed", User.GetUserId(), DateTime.UtcNow, null);
        Store[payment.Id] = payment;

        logger.LogInformation("Payment {PaymentId} for auction {AuctionId} processed: {Amount} {Currency} by {UserId}",
            payment.Id, payment.AuctionId, payment.Amount, payment.Currency, payment.ProcessedBy);
        return Created($"/api/payments/{payment.Id}", payment);
    }

    /// <summary>Повернути кошти за платежем (заглушка).</summary>
    [HttpPost("refund")]
    [RequirePermission(Permissions.Payments.Refund)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult Refund(RefundPaymentRequest request)
    {
        if (!Store.TryGetValue(request.PaymentId, out var payment))
            return Problem(statusCode: 404, title: $"Payment {request.PaymentId} not found");

        var refunded = payment with { Status = "Refunded", RefundedAt = DateTime.UtcNow };
        Store[payment.Id] = refunded;
        logger.LogInformation("Payment {PaymentId} refunded by {UserId}: {Reason}", payment.Id, User.GetUserId(), request.Reason);
        return Ok(refunded);
    }
}

public sealed record ProcessPaymentRequest(
    [Range(1, long.MaxValue)] long AuctionId,
    [Range(0.01, 1_000_000_000)] decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency);

public sealed record RefundPaymentRequest([Required] Guid PaymentId, [StringLength(500)] string? Reason);

public sealed record PaymentDto(Guid Id, long AuctionId, decimal Amount, string Currency, string Status,
    string? ProcessedBy, DateTime CreatedAt, DateTime? RefundedAt);
