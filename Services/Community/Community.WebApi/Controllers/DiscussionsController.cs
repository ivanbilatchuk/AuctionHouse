using AutoMapper;
using Community.Application.Commands.Discussions.AddComment;
using Community.Application.Commands.Discussions.CreateDiscussion;
using Community.Application.DTO;
using Community.Application.Queries.Discussions.GetDiscussionById;
using Community.WebApi.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Community.WebApi.Controllers;

/// <summary>Обговорення аукціонів із вбудованими коментарями та відповідями.</summary>
[ApiController]
[Route("api/discussions")]
[Produces("application/json")]
public sealed class DiscussionsController(ISender sender, IMapper mapper) : ControllerBase
{
    /// <summary>Обговорення з усіма коментарями й відповідями (один документ).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscussionDto>> GetById(string id, CancellationToken ct)
        => Ok(await sender.Send(new GetDiscussionByIdQuery(id), ct));

    /// <summary>Створює обговорення аукціону (одне на аукціон).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscussionDto>> Create(CreateDiscussionRequest request, CancellationToken ct)
    {
        var created = await sender.Send(mapper.Map<CreateDiscussionCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Додає коментар або (з parentCommentId) відповідь на коментар.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommentDto>> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        var comment = await sender.Send(mapper.Map<AddCommentCommand>(request) with { DiscussionId = id }, ct);
        return CreatedAtAction(nameof(GetById), new { id }, comment);
    }
}
