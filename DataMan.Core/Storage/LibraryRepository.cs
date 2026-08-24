using DataMan.Contracts;
using Microsoft.Data.Sqlite;

namespace DataMan.Core.Storage;

public sealed class LibraryRepository
{
    private readonly AppDatabase _database;

    public LibraryRepository(AppDatabase database)
    {
        _database = database;
    }

    public string EnsureLocalSource(string displayName, string? rootPath)
    {
        using var connection = _database.Open();
        var locator = rootPath is null
            ? null
            : new FileLocator { Path = Path.GetFullPath(rootPath), IsDirectory = true }.ToJson();

        using var find = connection.CreateCommand();
        find.CommandText = """
            SELECT source_id
            FROM sources
            WHERE type = 'local_filesystem'
              AND (
                    ($root IS NULL AND root_locator IS NULL)
                    OR json_extract(root_locator, '$.path') = json_extract($root, '$.path')
                  )
            LIMIT 1;
            """;
        find.Parameters.AddWithValue("$root", (object?)locator ?? DBNull.Value);
        if (find.ExecuteScalar() is string existing)
        {
            return existing;
        }

        var sourceId = Guid.NewGuid().ToString("D");
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO sources (source_id, type, display_name, root_locator, last_scan_at)
            VALUES ($source_id, 'local_filesystem', $display_name, $root_locator, $last_scan_at);
            """;
        insert.Parameters.AddWithValue("$source_id", sourceId);
        insert.Parameters.AddWithValue("$display_name", displayName);
        insert.Parameters.AddWithValue("$root_locator", (object?)locator ?? DBNull.Value);
        insert.Parameters.AddWithValue("$last_scan_at", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
        return sourceId;
    }

    public IReadOnlyList<ItemRecord> ListItems(int limit = 200)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT item_id, parent_item_id, source_id, kind, subtype, title,
                   content_hash, original_hash, locator, mime_type, size_bytes,
                   ingested_at, status
            FROM items
            WHERE kind != 'folder'
            ORDER BY ingested_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        return ReadItems(command);
    }

    public ItemDetail? GetItem(string itemId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.item_id, i.parent_item_id, i.source_id, i.kind, i.subtype, i.title,
                   i.content_hash, i.original_hash, i.locator, i.mime_type, i.size_bytes,
                   i.ingested_at, i.status, c.body, c.content_type
            FROM items i
            LEFT JOIN contents c ON c.item_id = i.item_id
            WHERE i.item_id = $item_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$item_id", itemId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ItemDetail
        {
            Item = ReadItem(reader),
            Body = reader.IsDBNull(13) ? null : reader.GetString(13),
            ContentType = reader.IsDBNull(14) ? null : reader.GetString(14)
        };
    }

    public SearchOutcome Search(LibraryQuery query)
    {
        return query.Match(
            recent => new SearchOutcome.Hits(ListAsHits(Clamp(recent.Limit))),
            lexical => new SearchOutcome.Hits(SearchFts(lexical.Text, Clamp(lexical.Limit))),
            semantic => throw new NotSupportedException("Semantic search is not wired yet."));
    }

    private IReadOnlyList<SearchHit> SearchFts(QueryText text, int limit)
    {
        var match = ToMatchQuery(text.Value);
        if (match is null)
        {
            return [];
        }

        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.item_id, i.parent_item_id, i.source_id, i.kind, i.subtype, i.title,
                   i.content_hash, i.original_hash, i.locator, i.mime_type, i.size_bytes,
                   i.ingested_at, i.status,
                   snippet(contents_fts, 0, '', '', '…', 24) AS snippet
            FROM contents_fts
            JOIN contents c ON c.rowid = contents_fts.rowid
            JOIN items i ON i.item_id = c.item_id
            WHERE contents_fts MATCH $query
            ORDER BY rank
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$query", match);
        command.Parameters.AddWithValue("$limit", limit);

        var hits = new List<SearchHit>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            hits.Add(new SearchHit
            {
                Item = ReadItem(reader),
                Snippet = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
        }

        return hits;
    }

    private IReadOnlyList<SearchHit> ListAsHits(int limit) =>
        ListItems(limit).Select(item => new SearchHit { Item = item }).ToArray();

    private static int Clamp(int limit) =>
        limit < 1 ? 1 : limit > 200 ? 200 : limit;

    public IReadOnlyList<ItemRecord> ListFileItemsBySource(string sourceId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT item_id, parent_item_id, source_id, kind, subtype, title,
                   content_hash, original_hash, locator, mime_type, size_bytes,
                   ingested_at, status
            FROM items
            WHERE source_id = $source_id
              AND kind != 'folder';
            """;
        command.Parameters.AddWithValue("$source_id", sourceId);
        return ReadItems(command);
    }

    public void UpdateItemLocation(string itemId, string locatorJson, string title, ItemStatus status)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE items
            SET locator = $locator,
                title = $title,
                status = $status,
                last_checked_at = $now
            WHERE item_id = $item_id;
            """;
        command.Parameters.AddWithValue("$item_id", itemId);
        command.Parameters.AddWithValue("$locator", locatorJson);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$status", ItemStatusCodec.ToStorage(status));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void UpdateItemStatus(string itemId, ItemStatus status)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE items
            SET status = $status,
                last_checked_at = $now
            WHERE item_id = $item_id;
            """;
        command.Parameters.AddWithValue("$item_id", itemId);
        command.Parameters.AddWithValue("$status", ItemStatusCodec.ToStorage(status));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void MarkSourceWatched(string sourceId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sources
            SET properties = '{"watch":true}'
            WHERE source_id = $source_id;
            """;
        command.Parameters.AddWithValue("$source_id", sourceId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<(string SourceId, string RootPath)> ListWatchedRoots()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_id, root_locator
            FROM sources
            WHERE type = 'local_filesystem'
              AND root_locator IS NOT NULL
              AND json_extract(properties, '$.watch') = 1;
            """;

        var roots = new List<(string SourceId, string RootPath)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var locator = FileLocator.Parse(reader.GetString(1));
                roots.Add((reader.GetString(0), locator.Path));
            }
            catch (InvalidOperationException)
            {
            }
        }

        return roots;
    }

    public void TouchLastScanAt(string sourceId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sources
            SET last_scan_at = $now
            WHERE source_id = $source_id;
            """;
        command.Parameters.AddWithValue("$source_id", sourceId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public LibraryStats GetStats()
    {
        using var connection = _database.Open();
        int Count(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar());
        }

        return new LibraryStats
        {
            ItemCount = Count("SELECT COUNT(*) FROM items WHERE kind != 'folder';"),
            SourceCount = Count("SELECT COUNT(*) FROM sources;"),
            ContentCount = Count("SELECT COUNT(*) FROM contents;"),
            DatabaseBytes = File.Exists(_database.DatabasePath) ? new FileInfo(_database.DatabasePath).Length : 0,
            DatabasePath = _database.DatabasePath,
            SchemaVersion = _database.ReadSchemaVersion()
        };
    }

    public int CountEmbeddings()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM embeddings;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static IReadOnlyList<ItemRecord> ReadItems(SqliteCommand command)
    {
        var items = new List<ItemRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static ItemRecord ReadItem(SqliteDataReader reader) => new()
    {
        ItemId = reader.GetString(0),
        ParentItemId = reader.IsDBNull(1) ? null : reader.GetString(1),
        SourceId = reader.GetString(2),
        Kind = reader.GetString(3),
        Subtype = reader.IsDBNull(4) ? null : reader.GetString(4),
        Title = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        ContentHash = reader.IsDBNull(6) ? null : reader.GetString(6),
        OriginalHash = reader.IsDBNull(7) ? null : reader.GetString(7),
        LocatorJson = reader.GetString(8),
        MimeType = reader.IsDBNull(9) ? null : reader.GetString(9),
        SizeBytes = reader.IsDBNull(10) ? null : reader.GetInt64(10),
        IngestedAt = reader.GetString(11),
        Status = ItemStatusCodec.FromStorage(reader.GetString(12))
    };

    private static string? ToMatchQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var tokens = raw
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()))
            .Where(token => token.Length > 0)
            .Select(token => token + "*")
            .ToArray();

        return tokens.Length == 0 ? null : string.Join(" AND ", tokens);
    }
}
