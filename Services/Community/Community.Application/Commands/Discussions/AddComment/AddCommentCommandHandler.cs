using AutoMapper;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using Community.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Community.Application.Commands.Discussions.AddComment;

public sealed class AddCommentCommandHandler(IDiscussionRepository repository, IMapper mapper)
    : IRequestHandler<AddCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        var discussion = await repository.GetByIdAsync(ObjectId.Parse(request.DiscussionId), cancellationToken)
            ?? throw new NotFoundException(nameof(Discussion), request.DiscussionId);

        var expectedVersion = discussion.Version;
        var author = new Person(request.AuthorUserId, request.AuthorName);

        var comment = request.ParentCommentId is null
            ? discussion.AddComment(author, request.Text)
            : discussion.Reply(ObjectId.Parse(request.ParentCommentId), author, request.Text);

        await repository.UpdateAsync(discussion, expectedVersion, cancellationToken);

        return mapper.Map<CommentDto>(comment);
    }
}
