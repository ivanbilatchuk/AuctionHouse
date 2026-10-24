using Community.Domain.Common;
using Community.Domain.Exceptions;
using Community.Domain.ValueObjects;
using MongoDB.Bson;

namespace Community.Domain.Entities;

// Обговорення аукціону: коментарі й відповіді вбудовані в документ і читаються одним запитом.
public sealed class Discussion : BaseEntity
{
    public const int MaxMessages = 500;

    private List<Comment> _comments = [];

    private Discussion() { }

    public Discussion(long auctionId, ItemReference item)
    {
        if (auctionId <= 0) throw new DomainException("Auction id must be positive.");

        AuctionId = auctionId;
        Item = item ?? throw new DomainException("Item is required.");
        Status = DiscussionStatus.Open;
    }

    public long AuctionId { get; private set; }

    public ItemReference Item { get; private set; } = null!;

    public DiscussionStatus Status { get; private set; }

    // Денормалізований лічильник коментарів і відповідей.
    public int CommentCount { get; private set; }

    public IReadOnlyList<Comment> Comments => _comments;

    public Comment AddComment(Person author, string text)
    {
        EnsureCanPost();

        var comment = new Comment(author, text);
        _comments.Add(comment);
        CommentCount++;
        MarkUpdated();
        return comment;
    }

    public Comment Reply(ObjectId commentId, Person author, string text)
    {
        EnsureCanPost();

        var parent = _comments.FirstOrDefault(c => c.Id == commentId)
            ?? throw new NotFoundException(nameof(Comment), commentId);

        var reply = parent.AddReply(author, text);
        CommentCount++;
        MarkUpdated();
        return reply;
    }

    public void Lock()
    {
        if (Status == DiscussionStatus.Locked) throw new DomainException("Discussion is already locked.");

        Status = DiscussionStatus.Locked;
        MarkUpdated();
    }

    private void EnsureCanPost()
    {
        if (Status == DiscussionStatus.Locked) throw new DomainException("Discussion is locked.");
        if (CommentCount >= MaxMessages) throw new DomainException($"Discussion reached the limit of {MaxMessages} messages.");
    }
}
