using Catalog.Api.Metrics;
using Catalog.Bll.DTO;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Catalog.Api.Controllers;

/// <summary>Лоти каталогу: перегляд із пагінацією, фільтрацією й сортуванням, створення та зміна стану.</summary>
[ApiController]
[Route("api/items")]
[Produces("application/json")]
public sealed class ItemsController(IItemService service, CatalogMetrics metrics) : ControllerBase
{
    /// <summary>Сторінка лотів. Фільтрація, сортування й Skip/Take виконуються в SQL.</summary>
    /// <remarks>Метадані сторінки повертаються в тілі (totalCount, totalPages, hasNext, hasPrevious) і в заголовках X-Total-Count / X-Total-Pages.</remarks>
    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ItemDto>>> GetList(
        [FromQuery] ItemQuery query,
        [FromServices] IValidator<ItemQuery> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.AddTo(ModelState));

        var page = await service.GetListAsync(query, ct);
        PaginationHeaders.Write(Response, page);
        return Ok(page);
    }

    /// <summary>Лоти продавця (друга специфікація через той самий WithSpecification()).</summary>
    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet("by-seller/{sellerId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ItemDto>>> GetBySeller(Guid sellerId, [FromQuery] PageRequest request, CancellationToken ct)
    {
        var page = await service.GetBySellerAsync(sellerId, request, ct);
        PaginationHeaders.Write(Response, page);
        return Ok(page);
    }

    /// <summary>Картка лота з деталями, зображеннями й тегами (Eager Loading, один SQL-запит).</summary>
    /// <summary>Лоти поточного користувача (продавець = claim "sub" токена). Токен сервісу (без sub) — 403.</summary>
    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ItemDto>>> GetMine([FromQuery] PageRequest request, CancellationToken ct)
    {
        if (User.GetUserGuid() is not { } userId)
            return Forbid();

        var page = await service.GetBySellerAsync(userId, request, ct);
        PaginationHeaders.Write(Response, page);
        return Ok(page);
    }

    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemDetailsDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    /// <summary>Створює лот. Повторний SKU або slug дає 409.</summary>
    [RequirePermission(Permissions.Catalog.Create)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemDetailsDto>> Create(
        CreateItemDto input,
        [FromServices] IValidator<CreateItemDto> validator,
        CancellationToken ct)
    {
        // Лаба 9: claims у бізнес-логіці — продавцем стає автентифікований користувач (sub), якщо продавця не вказано явно.
        if (input.SellerExternalId == Guid.Empty && User.GetUserGuid() is { } userId)
            input.SellerExternalId = userId;

        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.AddTo(ModelState));

        var created = await metrics.MeasureAsync("create_item", () => service.CreateAsync(input, ct));
        metrics.ItemCreated(created.Status);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Оновлює назву, slug, категорію та естімейт (лише Draft / Listed).</summary>
    [RequirePermission(Permissions.Catalog.Update)]
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        long id,
        UpdateItemDto input,
        [FromServices] IValidator<UpdateItemDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.AddTo(ModelState));

        await metrics.MeasureAsync("update_item", () => service.UpdateAsync(id, input, ct));
        metrics.ItemUpdated();
        return NoContent();
    }

    /// <summary>Змінює статус лота за правилами переходів доменної сутності.</summary>
    [RequirePermission(Permissions.Catalog.Publish)]
    [HttpPost("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        ChangeItemStatusDto input,
        [FromServices] IValidator<ChangeItemStatusDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.AddTo(ModelState));

        await metrics.MeasureAsync("change_item_status", () => service.ChangeStatusAsync(id, input, ct));
        metrics.ItemStatusChanged(input.Status);
        return NoContent();
    }

    /// <summary>Призначає тег лоту (рядок у проміжній таблиці ItemTag з AddedAt).</summary>
    [RequirePermission(Permissions.Catalog.Update)]
    [HttpPut("{id:long}/tags/{tagId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTag(long id, long tagId, CancellationToken ct)
    {
        await service.AddTagAsync(id, tagId, ct);
        return NoContent();
    }

    /// <summary>Знімає тег із лота.</summary>
    [RequirePermission(Permissions.Catalog.Update)]
    [HttpDelete("{id:long}/tags/{tagId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveTag(long id, long tagId, CancellationToken ct)
    {
        await service.RemoveTagAsync(id, tagId, ct);
        return NoContent();
    }

    /// <summary>М'яко видаляє лот (IsDeleted = 1).</summary>
    [RequirePermission(Permissions.Catalog.Delete)]
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("delete_item", () => service.DeleteAsync(id, ct));
        metrics.ItemDeleted();
        return NoContent();
    }
}
