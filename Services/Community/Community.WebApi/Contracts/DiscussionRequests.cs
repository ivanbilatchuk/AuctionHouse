namespace Community.WebApi.Contracts;

public sealed class CreateDiscussionRequest
{
    public long AuctionId { get; init; }
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
}

public sealed class AddCommentRequest
{
    public string? ParentCommentId { get; init; }
    public PersonRequest Author { get; init; } = new();
    public string Text { get; init; } = "";
}
