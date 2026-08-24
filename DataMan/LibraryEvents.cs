namespace DataMan;

public static class LibraryEvents
{
    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();
}
