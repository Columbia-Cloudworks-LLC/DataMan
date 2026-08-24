namespace DataMan.Core.Host;

public sealed class HostAppearance
{
    private const string FileName = "appearance";

    private readonly string _filePath;

    private HostAppearance(string filePath, Appearance current)
    {
        _filePath = filePath;
        Current = current;
    }

    public Appearance Current { get; private set; }

    public event Action<Appearance>? Changed;

    public static HostAppearance Open(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);
        var path = Path.Combine(dataRoot, FileName);
        string? raw = null;
        try
        {
            if (File.Exists(path))
            {
                raw = File.ReadAllText(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return new HostAppearance(path, Appearance.Parse(raw));
    }

    public void Select(Appearance appearance)
    {
        if (Equals(Current, appearance))
        {
            return;
        }

        var tmp = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tmp, appearance.Token + Environment.NewLine);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        Current = appearance;
        Changed?.Invoke(Current);
    }
}
