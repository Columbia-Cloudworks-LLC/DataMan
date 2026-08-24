namespace DataMan.Core.Search;

public sealed class FixedWindowChunker : ITextChunker
{
    public const int WindowChars = 512;
    public const int OverlapChars = 64;

    public IReadOnlyList<TextSpan> Split(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        var spans = new List<TextSpan>();
        var start = 0;
        var sequence = 0;
        while (start < body.Length)
        {
            var end = Math.Min(start + WindowChars, body.Length);
            spans.Add(new TextSpan(sequence++, body[start..end], start, end));
            if (end == body.Length)
            {
                break;
            }

            start = end - OverlapChars;
        }

        return spans;
    }
}
