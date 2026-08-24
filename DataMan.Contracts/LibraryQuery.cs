namespace DataMan.Contracts;

public readonly struct QueryText : IEquatable<QueryText>
{
    public string Value => _value ?? string.Empty;
    private readonly string? _value;

    private QueryText(string value) => _value = value;

    public static bool TryCreate(string? raw, out QueryText text)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.Any(char.IsLetterOrDigit))
        {
            text = default;
            return false;
        }

        text = new QueryText(trimmed);
        return true;
    }

    public static QueryText Parse(string raw) =>
        TryCreate(raw, out var text)
            ? text
            : throw new ArgumentException("Query text must be non-empty.", nameof(raw));

    public bool Equals(QueryText other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is QueryText other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}

public abstract record LibraryQuery
{
    private LibraryQuery()
    {
    }

    public sealed record Recent(int Limit = 50) : LibraryQuery;

    public sealed record Lexical(QueryText Text, int Limit = 50) : LibraryQuery;

    public sealed record Semantic(QueryText Text, int Limit = 50) : LibraryQuery;

    public T Match<T>(
        Func<Recent, T> recent,
        Func<Lexical, T> lexical,
        Func<Semantic, T> semantic) =>
        this switch
        {
            Recent r => recent(r),
            Lexical l => lexical(l),
            Semantic s => semantic(s),
            _ => throw new ArgumentOutOfRangeException(nameof(LibraryQuery))
        };
}

public enum SemanticGap
{
    EmbedderMissing,
    IndexEmpty
}

public abstract record SearchOutcome
{
    private SearchOutcome()
    {
    }

    public sealed record Hits(IReadOnlyList<SearchHit> Items) : SearchOutcome;

    public sealed record SemanticUnavailable(SemanticGap Gap) : SearchOutcome;
}
