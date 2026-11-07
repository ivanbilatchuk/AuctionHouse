using Community.Domain.Exceptions;
using Community.Domain.ValueObjects;
using MongoDB.Bson;

namespace Community.Domain.Entities;

// Коментар обговорення; відповіді вкладені в коментар (лише один рівень).
public sealed class Comment
{
    public const int MaxTextLength = 2000;

    private List<Comment> _replies = [];

    private Comment() { }

    internal Comment(Person author, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Comment text is required.");
        if (text.Length > MaxTextLength) throw new DomainException($"Comment must be at most {MaxTextLength} characters.");

        Id = ObjectId.GenerateNewId();
        Author = author ?? throw new DomainException("Author is required.");
        Text = text.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public ObjectId Id { get; private set; }

    public Person Author { get; private set; } = null!;

    public string Text { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<Comment> Replies => _replies;

    internal Comment AddReply(Person author, string text)
    {
        var reply = new Comment(author, text);
        _replies.Add(reply);
        return reply;
    }
}
