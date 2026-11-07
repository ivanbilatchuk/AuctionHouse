using AutoMapper;
using Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;
using Community.Application.Commands.ExpertOpinions.DeleteExpertOpinion;
using Community.Application.Commands.ExpertOpinions.UpdateExpertOpinion;
using Community.Application.Common;
using Community.Application.DTO;
using Community.Application.Queries.ExpertOpinions.GetExpertOpinionById;
using Community.Application.Queries.ExpertOpinions.GetExpertOpinions;
using Community.WebApi.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Community.WebApi.Controllers;

/// <summary>Висновки експертів про автентичність лотів (CQRS через MediatR).</summary>
[ApiController]
[Route("api/expert-opinions")]
[Produces("application/json")]
public sealed class ExpertOpinionsController(ISender sender, IMapper mapper) : ControllerBase
{
    /// <summary>Сторінка висновків: фільтри, повнотекстовий пошук і сортування в MongoDB (skip/limit).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ExpertOpinionListItemDto>>> GetList([FromQuery] ExpertOpinionListRequest request, CancellationToken ct)
        => Ok(await sender.Send(mapper.Map<GetExpertOpinionsQuery>(request), ct));

    /// <summary>Висновок за ObjectId.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpertOpinionDto>> GetById(string id, CancellationToken ct)
        => Ok(await sender.Send(new GetExpertOpinionByIdQuery(id), ct));

    /// <summary>Створює висновок. Location містить рядковий ObjectId.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExpertOpinionDto>> Create(CreateExpertOpinionRequest request, CancellationToken ct)
    {
        var created = await sender.Send(mapper.Map<CreateExpertOpinionCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Оновлює висновок; body.version має збігатися з поточною версією документа, інакше 409.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, UpdateExpertOpinionRequest request, CancellationToken ct)
    {
        await sender.Send(mapper.Map<UpdateExpertOpinionCommand>(request) with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Видаляє висновок.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await sender.Send(new DeleteExpertOpinionCommand(id), ct);
        return NoContent();
    }
}
