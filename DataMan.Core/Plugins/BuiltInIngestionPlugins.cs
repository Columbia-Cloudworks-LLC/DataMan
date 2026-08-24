using DataMan.Contracts;
using DataMan.Core.Ingestion;

namespace DataMan.Core.Plugins;

public static class BuiltInIngestionPlugins
{
    public static IReadOnlyList<IIngestionPlugin> CreateAll() =>
    [
        new PlainTextPlugin(),
        new MarkdownPlugin(),
        new LogFilePlugin()
    ];
}
