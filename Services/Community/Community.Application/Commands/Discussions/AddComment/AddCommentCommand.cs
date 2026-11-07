using Community.Application.Abstractions.Messaging;
using Community.Application.DTO;

namespace Community.Application.Commands.Discussions.AddComment;

public sealed record AddCommentCommand : ICommand<CommentDto>
{
    public string DiscussionId { get; init; } = "";

    // Якщо задано - це відповідь на коментар верхнього рівня.
    public string? ParentCommentId { get; init; }

    public Guid AuthorUserId { get; init; }
    public string AuthorName { get; init; } = "";
    public string Text { get; init; } = "";
}
