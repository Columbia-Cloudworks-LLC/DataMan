PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sources (
    source_id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    display_name TEXT,
    root_locator TEXT,
    auth_context_id TEXT,
    last_scan_at TEXT,
    properties TEXT
);

CREATE TABLE IF NOT EXISTS items (
    item_id TEXT PRIMARY KEY,
    parent_item_id TEXT REFERENCES items(item_id) ON DELETE SET NULL,
    source_id TEXT NOT NULL REFERENCES sources(source_id),
    kind TEXT NOT NULL,
    subtype TEXT,
    title TEXT,
    content_hash TEXT,
    original_hash TEXT,
    locator TEXT NOT NULL,
    mime_type TEXT,
    size_bytes INTEGER,
    source_created_at TEXT,
    source_updated_at TEXT,
    ingested_at TEXT NOT NULL,
    last_checked_at TEXT,
    status TEXT NOT NULL DEFAULT 'active',
    properties TEXT
);

CREATE TABLE IF NOT EXISTS contents (
    content_id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    content_type TEXT NOT NULL,
    body TEXT,
    language TEXT,
    extracted_by TEXT,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    properties TEXT
);

CREATE TABLE IF NOT EXISTS chunks (
    chunk_id TEXT PRIMARY KEY,
    content_id TEXT NOT NULL REFERENCES contents(content_id) ON DELETE CASCADE,
    item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    sequence INTEGER NOT NULL,
    text TEXT NOT NULL,
    token_count INTEGER,
    start_offset INTEGER,
    end_offset INTEGER,
    properties TEXT
);

CREATE TABLE IF NOT EXISTS embeddings (
    embedding_id TEXT PRIMARY KEY,
    chunk_id TEXT REFERENCES chunks(chunk_id) ON DELETE CASCADE,
    item_id TEXT REFERENCES items(item_id) ON DELETE CASCADE,
    model TEXT NOT NULL,
    dimensions INTEGER NOT NULL,
    vector BLOB NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS tags (
    tag_id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    parent_tag_id TEXT REFERENCES tags(tag_id),
    description TEXT,
    embedding_id TEXT
);

CREATE TABLE IF NOT EXISTS item_tags (
    item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    tag_id TEXT NOT NULL REFERENCES tags(tag_id) ON DELETE CASCADE,
    confidence REAL,
    assigned_by TEXT,
    PRIMARY KEY (item_id, tag_id)
);

CREATE TABLE IF NOT EXISTS relationships (
    relationship_id TEXT PRIMARY KEY,
    from_item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    to_item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    relation_type TEXT NOT NULL,
    properties TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ingestion_runs (
    run_id TEXT PRIMARY KEY,
    source_id TEXT REFERENCES sources(source_id),
    plugin_id TEXT NOT NULL,
    started_at TEXT NOT NULL,
    finished_at TEXT,
    status TEXT,
    items_processed INTEGER,
    error_summary TEXT,
    properties TEXT
);

CREATE TABLE IF NOT EXISTS item_versions (
    version_id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    content_hash TEXT,
    locator TEXT,
    captured_at TEXT NOT NULL,
    properties TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_items_source_path
    ON items(source_id, json_extract(locator, '$.path'));
CREATE INDEX IF NOT EXISTS idx_items_source ON items(source_id);
CREATE INDEX IF NOT EXISTS idx_items_kind ON items(kind);
CREATE INDEX IF NOT EXISTS idx_items_status ON items(status);
CREATE INDEX IF NOT EXISTS idx_items_parent ON items(parent_item_id);
CREATE INDEX IF NOT EXISTS idx_items_hash ON items(original_hash);
CREATE INDEX IF NOT EXISTS idx_contents_item ON contents(item_id);
CREATE INDEX IF NOT EXISTS idx_chunks_item ON chunks(item_id);
CREATE INDEX IF NOT EXISTS idx_chunks_content ON chunks(content_id);
CREATE INDEX IF NOT EXISTS idx_embeddings_chunk ON embeddings(chunk_id);
CREATE INDEX IF NOT EXISTS idx_embeddings_model ON embeddings(model);
CREATE INDEX IF NOT EXISTS idx_relationships_from ON relationships(from_item_id);
CREATE INDEX IF NOT EXISTS idx_relationships_to ON relationships(to_item_id);

CREATE VIRTUAL TABLE IF NOT EXISTS contents_fts USING fts5(
    body,
    content='contents',
    content_rowid='rowid'
);

CREATE TRIGGER IF NOT EXISTS contents_ai AFTER INSERT ON contents BEGIN
    INSERT INTO contents_fts(rowid, body) VALUES (new.rowid, new.body);
END;

CREATE TRIGGER IF NOT EXISTS contents_ad AFTER DELETE ON contents BEGIN
    INSERT INTO contents_fts(contents_fts, rowid, body) VALUES('delete', old.rowid, old.body);
END;

CREATE TRIGGER IF NOT EXISTS contents_au AFTER UPDATE ON contents BEGIN
    INSERT INTO contents_fts(contents_fts, rowid, body) VALUES('delete', old.rowid, old.body);
    INSERT INTO contents_fts(rowid, body) VALUES (new.rowid, new.body);
END;

INSERT OR IGNORE INTO meta(key, value) VALUES ('schema_version', '1');
