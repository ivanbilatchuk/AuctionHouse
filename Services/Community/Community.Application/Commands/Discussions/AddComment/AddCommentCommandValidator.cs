using Community.Application.Validation;
using Community.Domain.Entities;
using FluentValidation;

namespace Community.Application.Commands.Discussions.AddComment;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.DiscussionId).MustBeObjectId();
        RuleFor(x => x.ParentCommentId).MustBeObjectIdIfPresent();
        RuleFor(x => x.AuthorUserId).NotEmpty();
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.MaxTextLength);
    }
}
