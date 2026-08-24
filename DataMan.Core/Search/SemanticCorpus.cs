using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DataMan.Core.Search;

internal readonly record struct ItemNeighbor(
    string ItemId,
    string Snippet,
    float Score);

internal abstract record SemanticLookup
{
    private SemanticLookup()
    {
    }

    public sealed record Found(IReadOnlyList<ItemNeighbor> Items) : SemanticLookup;

    public sealed record EmbedderMissing : SemanticLookup;

    public sealed record EmptyIndex : SemanticLookup;
}

public sealed class SemanticCorpus
{
    private readonly AppDatabase _database;
    private readonly ITextChunker _chunker;
    private readonly ITextEmbedder _embedder;

    public SemanticCorpus(AppDatabase database, ITextChunker chunker, ITextEmbedder embedder)
    {
        _database = database;
        _chunker = chunker;
        _embedder = embedder;
    }

    public void Invalidate(SqliteConnection connection, string contentId)
    {
        using (var deleteEmbeddings = connection.CreateCommand())
        {
            deleteEmbeddings.CommandText = """
                DELETE FROM embeddings
                WHERE chunk_id IN (SELECT chunk_id FROM chunks WHERE content_id = $content_id);
                """;
            deleteEmbeddings.Parameters.AddWithValue("$content_id", contentId);
            deleteEmbeddings.ExecuteNonQuery();
        }

        using var deleteChunks = connection.CreateCommand();
        deleteChunks.CommandText = "DELETE FROM chunks WHERE content_id = $content_id;";
        deleteChunks.Parameters.AddWithValue("$content_id", contentId);
        deleteChunks.ExecuteNonQuery();
    }

    public Task IndexAsync(string contentId, string itemId, string body, CancellationToken cancellationToken)
    {
        if (!_embedder.IsAvailable || string.IsNullOrEmpty(body))
        {
            return Task.CompletedTask;
        }

        var spans = _chunker.Split(body);
        if (spans.Count == 0)
        {
            return Task.CompletedTask;
        }

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var span in spans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = _embedder.TryEmbed(span.Text);
            if (vector is null)
            {
                continue;
            }

            var chunkId = Guid.NewGuid().ToString("D");
            using (var insertChunk = connection.CreateCommand())
            {
                insertChunk.CommandText = """
                    INSERT INTO chunks (
                        chunk_id, content_id, item_id, sequence, text, token_count, start_offset, end_offset
                    ) VALUES (
                        $chunk_id, $content_id, $item_id, $sequence, $text, $token_count, $start_offset, $end_offset
                    );
                    """;
                insertChunk.Parameters.AddWithValue("$chunk_id", chunkId);
                insertChunk.Parameters.AddWithValue("$content_id", contentId);
                insertChunk.Parameters.AddWithValue("$item_id", itemId);
                insertChunk.Parameters.AddWithValue("$sequence", span.Sequence);
                insertChunk.Parameters.AddWithValue("$text", span.Text);
                insertChunk.Parameters.AddWithValue("$token_count", span.Text.Length);
                insertChunk.Parameters.AddWithValue("$start_offset", span.StartOffset);
                insertChunk.Parameters.AddWithValue("$end_offset", span.EndOffset);
                insertChunk.ExecuteNonQuery();
            }

            using var insertEmbedding = connection.CreateCommand();
            insertEmbedding.CommandText = """
                INSERT INTO embeddings (
                    embedding_id, chunk_id, item_id, model, dimensions, vector, created_at
                ) VALUES (
                    $embedding_id, $chunk_id, $item_id, $model, $dimensions, $vector, $created_at
                );
                """;
            insertEmbedding.Parameters.AddWithValue("$embedding_id", Guid.NewGuid().ToString("D"));
            insertEmbedding.Parameters.AddWithValue("$chunk_id", chunkId);
            insertEmbedding.Parameters.AddWithValue("$item_id", itemId);
            insertEmbedding.Parameters.AddWithValue("$model", vector.Value.Model.Id);
            insertEmbedding.Parameters.AddWithValue("$dimensions", vector.Value.Model.Dimensions);
            insertEmbedding.Parameters.Add("$vector", SqliteType.Blob).Value = EmbeddingBlob.ToBlob(vector.Value);
            insertEmbedding.Parameters.AddWithValue("$created_at", now);
            insertEmbedding.ExecuteNonQuery();
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    internal SemanticLookup TryNearestItems(QueryText text, int limit)
    {
        if (!_embedder.IsAvailable)
        {
            return new SemanticLookup.EmbedderMissing();
        }

        var queryVector = _embedder.TryEmbed(text.Value);
        if (queryVector is null)
        {
            return new SemanticLookup.EmbedderMissing();
        }

        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.item_id, e.vector, e.dimensions, e.model, c.text
            FROM embeddings e
            JOIN chunks c ON c.chunk_id = e.chunk_id
            JOIN items i ON i.item_id = e.item_id
            WHERE e.model = $model
              AND i.kind != 'folder'
              LIMIT 5000;
            """;
        command.Parameters.AddWithValue("$model", _embedder.Model.Id);

        var best = new Dictionary<string, ItemNeighbor>(StringComparer.Ordinal);
        var hadRows = false;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            hadRows = true;
            var itemId = reader.GetString(0);
            var blob = (byte[])reader[1];
            var dimensions = reader.GetInt32(2);
            var modelId = reader.GetString(3);
            var snippet = reader.GetString(4);
            var stored = new EmbeddingVector(new EmbeddingModel(modelId, dimensions), EmbeddingBlob.FromBlob(blob, dimensions));
            var score = EmbeddingBlob.Cosine(queryVector.Value, stored);
            if (score <= 0)
            {
                continue;
            }

            if (!best.TryGetValue(itemId, out var current) || score > current.Score)
            {
                best[itemId] = new ItemNeighbor(itemId, snippet, score);
            }
        }

        if (!hadRows)
        {
            return new SemanticLookup.EmptyIndex();
        }

        var neighbors = best.Values
            .OrderByDescending(neighbor => neighbor.Score)
            .ThenBy(neighbor => neighbor.ItemId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        return new SemanticLookup.Found(neighbors);
    }
}
