namespace Community.Application.DTO;

public sealed class DiscussionDto
{
    public string Id { get; init; } = "";
    public long AuctionId { get; init; }
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string Status { get; init; } = "";
    public int CommentCount { get; init; }
    public List<CommentDto> Comments { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Version { get; init; }
}

public sealed class CommentDto
{
    public string Id { get; init; } = "";
    public PersonDto Author { get; init; } = new();
    public string Text { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public List<CommentDto> Replies { get; init; } = [];
}
