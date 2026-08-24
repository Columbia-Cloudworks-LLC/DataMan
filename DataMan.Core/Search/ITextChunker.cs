namespace DataMan.Core.Search;

public sealed record TextSpan(
    int Sequence,
    string Text,
    int StartOffset,
    int EndOffset);

public interface ITextChunker
{
    IReadOnlyList<TextSpan> Split(string body);
}
