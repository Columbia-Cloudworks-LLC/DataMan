# DataMan

A local WinUI 3 knowledge base. You ingest files. The host stores locators, hashes, and extracted text in SQLite. Original binaries stay on disk.

Copyright 2026 Columbia Cloudworks LLC. Licensed under the [Apache License 2.0](LICENSE).

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

Windows x64 is the packaged WinUI ship path. Linux x64 runs `DataMan.Desktop` and publishes a self-contained tarball. Merges to `main` that include `feat` or `fix` commits produce a Release Please pull request. Merging that request publishes `DataMan-X.Y.Z-win-x64.zip` on GitHub Releases.

This tree implements the Phase 0 and Phase 1 slice, plus Phase 2 plugin loading. Drop `.txt`, `.md`, or `.log` files, browse them, and search the extracted text. A third-party plugin that only references `DataMan.Contracts` can add another format from `%LocalAppData%\DataMan\plugins`.

## Run in Cursor

Install the recommended C# and C# Dev Kit extensions when Cursor prompts.

- Terminal > Run Task > `build` compiles Contracts, Core, Tests, then the x64 WinUI host.
- Terminal > Run Task > `test` runs `scripts\verify-ship.ps1`.
- Terminal > Run Task > `run` builds, then starts the unpackaged exe.
- Run and Debug > `Debug DataMan` builds, then launches that same exe under the .NET debugger.

The database is `%LocalAppData%\DataMan\dataman.db`. Plugins live under `%LocalAppData%\DataMan\plugins\<id>\` with `plugin.json` or `bundle.json`. After an agent edits a `.cs`, `.xaml`, or `.csproj` file, `.cursor/hooks/after-file-edit.ps1` runs `scripts\check-edited-file.ps1` to format and compile the owning project.

Visual Studio can still deploy `DataMan (Package)` if you want MSIX. That path was not exercised here.

## Verify

```powershell
.\scripts\verify-ship.ps1
```

That script runs the ingest and search tests, builds the host, and checks the Cursor task, launch, and hook wiring. `.\scripts\publish-win-x64.ps1` writes `artifacts\DataMan-X.Y.Z-win-x64.zip`, using the version from `Directory.Build.props`.

## Linux

Ubuntu 24.04 with the .NET 8 SDK (8.0.4xx):

```bash
dotnet run --project DataMan.Desktop
./scripts/verify-linux.sh
./scripts/publish-linux-x64.sh
```

`verify-linux.sh` runs `DataMan.Tests`, builds `DataMan.Desktop`, and publishes a self-contained linux-x64 binary. `publish-linux-x64.sh` writes `artifacts/DataMan-X.Y.Z-linux-x64.tar.gz`.

The database is `$XDG_DATA_HOME/DataMan/dataman.db`, or `~/.local/share/DataMan/dataman.db` when `XDG_DATA_HOME` is unset. Plugins live under that same DataMan directory.

## What is in

- Contract assembly (`IIngestionPlugin`, `IItemWriter`, locators)
- SQLite schema from the design doc, including FTS5
- Built-in plugins for `.txt`, `.md`, and `.log`
- Directory discovery, collectible `AssemblyLoadContext` load and unload, and nested bundle flatten
- Dashboard with drag-and-drop and file/folder pickers
- Browser with list, detail, and full-text search
- Local file moves and renames on a watched or scanned folder keep the same `item_id` via `original_hash`

## What is not

Embeddings, MCP, the SQL editor, and cloud sources stay for later Phase 2 work.
