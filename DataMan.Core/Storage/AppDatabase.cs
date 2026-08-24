using Microsoft.Data.Sqlite;

namespace DataMan.Core.Storage;

public sealed class AppDatabase : IDisposable
{
    public const int CurrentSchemaVersion = 1;

    private readonly string _connectionString;

    public AppDatabase(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = LoadSchemaSql();
        command.ExecuteNonQuery();
    }

    public int ReadSchemaVersion()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = 'schema_version';";
        var value = command.ExecuteScalar()?.ToString();
        return int.TryParse(value, out var version) ? version : 0;
    }

    public static string LoadSchemaSql()
    {
        var assembly = typeof(AppDatabase).Assembly;
        var name = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("Schema.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Schema.sql embedded resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
    }
}
