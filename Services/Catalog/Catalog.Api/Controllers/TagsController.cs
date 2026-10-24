using Catalog.Bll.DTO;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Теги лотів і вибірка M:N через проміжну сутність ItemTag.</summary>
[ApiController]
[Route("api/tags")]
[Produces("application/json")]
public sealed class TagsController(ITagService service) : ControllerBase
{
    /// <summary>Усі теги.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    /// <summary>Лоти, яким тег призначено після вказаної дати (LINQ-запит по ItemTag.AddedAt).</summary>
    /// <param name="slug">Slug тега, наприклад rare.</param>
    /// <param name="since">Нижня межа AddedAt (UTC); за замовчуванням останні 30 днів.</param>
    /// <param name="ct">Токен скасування.</param>
    [HttpGet("{slug}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaggedItemDto>>> GetRecentlyTagged(string slug, [FromQuery] DateTime? since, CancellationToken ct)
        => Ok(await service.GetRecentlyTaggedAsync(slug, since, ct));
}
