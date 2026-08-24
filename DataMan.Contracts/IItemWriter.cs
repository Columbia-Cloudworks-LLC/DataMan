namespace DataMan.Contracts;

public interface IItemWriter
{
    Task<string> UpsertItemAsync(ItemDraft item, ContentDraft? content, CancellationToken cancellationToken);
}
