using DataMan.Contracts;
using DataMan.Core.Search;
using Microsoft.Data.Sqlite;

namespace DataMan.Core.Storage;

internal readonly record struct ContentCommit(string ContentId, string ItemId, string Body);

public sealed class SqliteItemWriter : IItemWriter
{
    private readonly AppDatabase _database;
    private readonly SemanticCorpus _corpus;

    public SqliteItemWriter(AppDatabase database, SemanticCorpus corpus)
    {
        _database = database;
        _corpus = corpus;
    }

    public async Task<string> UpsertItemAsync(ItemDraft item, ContentDraft? content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ContentCommit? committed = null;
        string itemId;
        using (var connection = _database.Open())
        using (var transaction = connection.BeginTransaction())
        {
            var existingId = FindItemId(connection, item.SourceId, item.LocatorJson);
            itemId = existingId ?? Guid.NewGuid().ToString("D");
            var now = DateTimeOffset.UtcNow.ToString("O");

            if (existingId is null)
            {
                InsertItem(connection, itemId, item, now);
            }
            else
            {
                UpdateItem(connection, itemId, item, now);
            }

            if (content is not null)
            {
                committed = UpsertContent(connection, itemId, content, now);
                _corpus.Invalidate(connection, committed.Value.ContentId);
            }

            transaction.Commit();
        }

        if (committed is { } commit)
        {
            try
            {
                await _corpus.IndexAsync(commit.ContentId, itemId, commit.Body, cancellationToken);
            }
            catch (Exception)
            {
                // Persistence has already committed; indexing is best-effort.
            }
        }

        return itemId;
    }

    private static string? FindItemId(SqliteConnection connection, string sourceId, string locatorJson)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT item_id
            FROM items
            WHERE source_id = $source_id
              AND json_extract(locator, '$.path') = json_extract($locator, '$.path')
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$source_id", sourceId);
        command.Parameters.AddWithValue("$locator", locatorJson);
        return command.ExecuteScalar() as string;
    }

    private static void InsertItem(SqliteConnection connection, string itemId, ItemDraft item, string now)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO items (
                item_id, parent_item_id, source_id, kind, subtype, title,
                content_hash, original_hash, locator, mime_type, size_bytes,
                source_created_at, source_updated_at, ingested_at, last_checked_at, status
            ) VALUES (
                $item_id, $parent_item_id, $source_id, $kind, $subtype, $title,
                $content_hash, $original_hash, $locator, $mime_type, $size_bytes,
                $source_created_at, $source_updated_at, $ingested_at, $last_checked_at, $status
            );
            """;
        BindItem(command, itemId, item, now);
        command.ExecuteNonQuery();
    }

    private static void UpdateItem(SqliteConnection connection, string itemId, ItemDraft item, string now)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE items SET
                parent_item_id = $parent_item_id,
                kind = $kind,
                subtype = $subtype,
                title = $title,
                content_hash = $content_hash,
                original_hash = $original_hash,
                locator = $locator,
                mime_type = $mime_type,
                size_bytes = $size_bytes,
                source_created_at = $source_created_at,
                source_updated_at = $source_updated_at,
                last_checked_at = $last_checked_at,
                status = $status
            WHERE item_id = $item_id;
            """;
        BindItem(command, itemId, item, now);
        command.ExecuteNonQuery();
    }

    private static void BindItem(SqliteCommand command, string itemId, ItemDraft item, string now)
    {
        command.Parameters.AddWithValue("$item_id", itemId);
        command.Parameters.AddWithValue("$parent_item_id", (object?)item.ParentItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source_id", item.SourceId);
        command.Parameters.AddWithValue("$kind", item.Kind);
        command.Parameters.AddWithValue("$subtype", (object?)item.Subtype ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$content_hash", (object?)item.ContentHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$original_hash", (object?)item.OriginalHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$locator", item.LocatorJson);
        command.Parameters.AddWithValue("$mime_type", (object?)item.MimeType ?? DBNull.Value);
        command.Parameters.AddWithValue("$size_bytes", (object?)item.SizeBytes ?? DBNull.Value);
        command.Parameters.AddWithValue("$source_created_at", (object?)item.SourceCreatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$source_updated_at", (object?)item.SourceUpdatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$ingested_at", now);
        command.Parameters.AddWithValue("$last_checked_at", now);
        command.Parameters.AddWithValue("$status", ItemStatusCodec.ToStorage(item.Status));
    }

    private static ContentCommit UpsertContent(SqliteConnection connection, string itemId, ContentDraft content, string now)
    {
        using var find = connection.CreateCommand();
        find.CommandText = "SELECT content_id, version FROM contents WHERE item_id = $item_id LIMIT 1;";
        find.Parameters.AddWithValue("$item_id", itemId);

        string contentId;
        int version;
        using (var reader = find.ExecuteReader())
        {
            if (reader.Read())
            {
                contentId = reader.GetString(0);
                version = reader.GetInt32(1) + 1;
            }
            else
            {
                contentId = Guid.NewGuid().ToString("D");
                version = 1;
            }
        }

        using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO contents (
                content_id, item_id, content_type, body, language, extracted_by, version, created_at
            ) VALUES (
                $content_id, $item_id, $content_type, $body, $language, $extracted_by, $version, $created_at
            )
            ON CONFLICT(content_id) DO UPDATE SET
                content_type = excluded.content_type,
                body = excluded.body,
                language = excluded.language,
                extracted_by = excluded.extracted_by,
                version = excluded.version;
            """;
        upsert.Parameters.AddWithValue("$content_id", contentId);
        upsert.Parameters.AddWithValue("$item_id", itemId);
        upsert.Parameters.AddWithValue("$content_type", content.ContentType);
        upsert.Parameters.AddWithValue("$body", content.Body);
        upsert.Parameters.AddWithValue("$language", (object?)content.Language ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$extracted_by", content.ExtractedBy);
        upsert.Parameters.AddWithValue("$version", version);
        upsert.Parameters.AddWithValue("$created_at", now);
        upsert.ExecuteNonQuery();
        return new ContentCommit(contentId, itemId, content.Body);
    }
}
