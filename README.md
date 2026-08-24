# DataMan

A local WinUI 3 knowledge base. You ingest files. The host stores locators, hashes, and extracted text in SQLite. Original binaries stay on disk.

Copyright 2026 Columbia Cloudworks LLC. Licensed under the [Apache License 2.0](LICENSE).

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

Windows x64 is the only supported package. Merges to `main` that include `feat` or `fix` commits produce a Release Please pull request. Merging that request publishes `DataMan-X.Y.Z-win-x64.zip` on GitHub Releases.

This tree implements the Phase 0 and Phase 1 slice from the design docs. Drop `.txt`, `.md`, or `.log` files, browse them, and search the extracted text.

## Run in Cursor

Install the recommended C# and C# Dev Kit extensions when Cursor prompts.

- Terminal > Run Task > `build` compiles Contracts, Core, Tests, then the x64 WinUI host.
- Terminal > Run Task > `test` runs `scripts\verify-mvp.ps1`.
- Terminal > Run Task > `run` builds, then starts the unpackaged exe.
- Run and Debug > `Debug DataMan` builds, then launches that same exe under the .NET debugger.

The database is `%LocalAppData%\DataMan\dataman.db`. After an agent edits a `.cs`, `.xaml`, or `.csproj` file, `.cursor/hooks/after-file-edit.ps1` runs `scripts\check-edited-file.ps1` to format and compile the owning project.

Visual Studio can still deploy `DataMan (Package)` if you want MSIX. That path was not exercised here.

## Verify

```powershell
.\scripts\verify-mvp.ps1
.\scripts\verify-cursor-setup.ps1
```

The first script runs ingest and search tests, then builds the host. The second checks the Cursor task, launch, and hook wiring. `.\scripts\publish-win-x64.ps1` writes `artifacts\DataMan-X.Y.Z-win-x64.zip`, using the version from `Directory.Build.props`.

## What is in

- Contract assembly (`IIngestionPlugin`, `IItemWriter`, locators)
- SQLite schema from the design doc, including FTS5
- In-process plugins for `.txt`, `.md`, and `.log`
- Dashboard with drag-and-drop and file/folder pickers
- Browser with list, detail, and full-text search

## What is not

Dynamic plugin loading, embeddings, MCP, the SQL editor, and cloud sources stay for later phases.
