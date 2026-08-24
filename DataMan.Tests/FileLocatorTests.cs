using DataMan.Contracts;
using Xunit;

namespace DataMan.Tests;

public sealed class FileLocatorTests
{
    [Fact]
    public void Round_trips_file_path()
    {
        var locator = new FileLocator { Path = @"C:\Users\you\notes.md" };
        var parsed = FileLocator.Parse(locator.ToJson());
        Assert.Equal("file", parsed.Scheme);
        Assert.Equal(@"C:\Users\you\notes.md", parsed.Path);
        Assert.False(parsed.IsDirectory);
    }

    [Fact]
    public void Rejects_unknown_scheme()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FileLocator.Parse("""{"scheme":"youtube","path":"x"}"""));
    }
}
