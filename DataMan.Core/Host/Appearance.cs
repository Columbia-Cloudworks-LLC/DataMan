namespace DataMan.Core.Host;

public abstract record Appearance
{
    private Appearance()
    {
    }

    public sealed record System : Appearance;

    public sealed record Light : Appearance;

    public sealed record Dark : Appearance;

    public T Match<T>(
        Func<System, T> system,
        Func<Light, T> light,
        Func<Dark, T> dark) =>
        this switch
        {
            System s => system(s),
            Light l => light(l),
            Dark d => dark(d),
            _ => throw new ArgumentOutOfRangeException(nameof(Appearance))
        };

    public static Appearance Parse(string? raw)
    {
        var token = raw?.Trim().ToLowerInvariant();
        return token switch
        {
            "light" => new Light(),
            "dark" => new Dark(),
            _ => new System()
        };
    }

    internal string Token => Match(_ => "system", _ => "light", _ => "dark");
}
