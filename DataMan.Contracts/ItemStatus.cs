namespace DataMan.Contracts;

public enum ItemStatus
{
    Active,
    Moved,
    Missing,
    Deleted,
    Pending,
    Error
}

public static class ItemStatusCodec
{
    public static string ToStorage(ItemStatus status) => status switch
    {
        ItemStatus.Active => "active",
        ItemStatus.Moved => "moved",
        ItemStatus.Missing => "missing",
        ItemStatus.Deleted => "deleted",
        ItemStatus.Pending => "pending",
        ItemStatus.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ItemStatus FromStorage(string value) => value switch
    {
        "active" => ItemStatus.Active,
        "moved" => ItemStatus.Moved,
        "missing" => ItemStatus.Missing,
        "deleted" => ItemStatus.Deleted,
        "pending" => ItemStatus.Pending,
        "error" => ItemStatus.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
