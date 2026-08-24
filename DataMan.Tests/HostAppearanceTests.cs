using DataMan.Core.Host;
using Xunit;

namespace DataMan.Tests;

public sealed class HostAppearanceTests : IDisposable
{
    private readonly string _root;

    public HostAppearanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dataman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Open_without_file_is_system()
    {
        var host = HostAppearance.Open(_root);
        Assert.IsType<Appearance.System>(host.Current);
    }

    [Fact]
    public void Parse_unknown_is_system()
    {
        Assert.IsType<Appearance.System>(Appearance.Parse("solarized"));
        Assert.IsType<Appearance.System>(Appearance.Parse(null));
        Assert.IsType<Appearance.System>(Appearance.Parse(""));
        Assert.IsType<Appearance.System>(Appearance.Parse("   "));
        Assert.IsType<Appearance.Dark>(Appearance.Parse("DARK"));
        Assert.IsType<Appearance.Dark>(Appearance.Parse("dark\n"));
        Assert.IsType<Appearance.Light>(Appearance.Parse("light"));
    }

    [Fact]
    public void Select_round_trips_across_open()
    {
        HostAppearance.Open(_root).Select(new Appearance.Dark());
        Assert.IsType<Appearance.Dark>(HostAppearance.Open(_root).Current);
        Assert.Equal("dark", File.ReadAllText(Path.Combine(_root, "appearance")).Trim());
    }

    [Fact]
    public void Select_same_value_does_not_raise_changed()
    {
        var host = HostAppearance.Open(_root);
        var raised = 0;
        host.Changed += _ => raised++;
        host.Select(host.Current);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Corrupt_file_defaults_to_system()
    {
        File.WriteAllText(Path.Combine(_root, "appearance"), "{not-a-token}");
        Assert.IsType<Appearance.System>(HostAppearance.Open(_root).Current);
    }

    [Fact]
    public void Select_io_failure_keeps_current_and_does_not_throw()
    {
        var host = HostAppearance.Open(_root);
        var raised = 0;
        host.Changed += _ => raised++;
        Directory.CreateDirectory(Path.Combine(_root, "appearance.tmp"));

        host.Select(new Appearance.Dark());

        Assert.IsType<Appearance.System>(host.Current);
        Assert.Equal(0, raised);
        Assert.False(File.Exists(Path.Combine(_root, "appearance")));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
