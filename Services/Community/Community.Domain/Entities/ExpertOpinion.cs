using Community.Domain.Common;
using Community.Domain.Exceptions;
using Community.Domain.ValueObjects;

namespace Community.Domain.Entities;

// Висновок експерта про автентичність лота. Набір атрибутів залежить від категорії (гнучка схема).
public sealed class ExpertOpinion : BaseEntity
{
    public const int MaxNotesLength = 4000;

    public static readonly IReadOnlyDictionary<CategoryKind, string[]> RequiredAttributes =
        new Dictionary<CategoryKind, string[]>
        {
            [CategoryKind.Cards] = ["gradingCompany", "grade"],
            [CategoryKind.Stamps] = ["perforation", "watermark"],
            [CategoryKind.Vinyl] = ["matrixNumber", "pressing"],
            [CategoryKind.Coins] = ["metal", "weightGrams"]
        };

    private Dictionary<string, string> _attributes = new();

    private ExpertOpinion() { }

    public ExpertOpinion(ItemReference item, CategoryKind categoryKind, Person expert, Verdict verdict,
        Confidence confidence, IReadOnlyDictionary<string, string> attributes, string? notes)
    {
        Item = item ?? throw new DomainException("Item is required.");
        Expert = expert ?? throw new DomainException("Expert is required.");
        CategoryKind = categoryKind;
        Apply(verdict, confidence, attributes, notes);
    }

    public ItemReference Item { get; private set; } = null!;

    public CategoryKind CategoryKind { get; private set; }

    public Person Expert { get; private set; } = null!;

    public Verdict Verdict { get; private set; }

    public Confidence Confidence { get; private set; } = null!;

    public string? Notes { get; private set; }

    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    public void Revise(Verdict verdict, Confidence confidence, IReadOnlyDictionary<string, string> attributes, string? notes)
    {
        Apply(verdict, confidence, attributes, notes);
        MarkUpdated();
    }

    private void Apply(Verdict verdict, Confidence confidence, IReadOnlyDictionary<string, string> attributes, string? notes)
    {
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(attributes);

        var missing = RequiredAttributes[CategoryKind].Where(key => !attributes.ContainsKey(key)).ToArray();
        if (missing.Length > 0)
            throw new DomainException($"Attributes for {CategoryKind} must contain: {string.Join(", ", missing)}.");

        if (verdict != Verdict.Authentic && string.IsNullOrWhiteSpace(notes))
            throw new DomainException($"Verdict '{verdict}' must be justified in notes.");

        if (notes?.Length > MaxNotesLength)
            throw new DomainException($"Notes must be at most {MaxNotesLength} characters.");

        Verdict = verdict;
        Confidence = confidence;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        _attributes.Clear();
        foreach (var (key, value) in attributes)
            _attributes[key] = value;
    }
}
