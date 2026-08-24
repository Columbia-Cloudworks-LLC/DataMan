# How to contribute

This repository is the Windows x64 host. Other platforms are out of scope.

## Build

Install the .NET 8 SDK and the C# / C# Dev Kit extensions in Cursor.

```powershell
.\scripts\dev-build.ps1
.\scripts\verify-mvp.ps1
```

`Terminal > Run Task > build` and `Debug DataMan` use the same unpackaged x64 exe.

## Pull requests

1. Branch from `main`.
2. Keep the change one concern.
3. Use [Conventional Commits](https://www.conventionalcommits.org/) in the commit subject.
4. Run `.\scripts\verify-mvp.ps1` before you open the pull request.

Subjects look like `feat(ingest): keep folder items on drop` or `fix(search): escape FTS tokens`.
`feat` and `fix` are what Release Please uses to open the next version pull request.

## Release

Do not tag by hand for a normal ship.

1. Merge work to `main`. CI must stay green.
2. Release Please opens or updates a release pull request. It rewrites `CHANGELOG.md` and `Directory.Build.props`.
3. Merge that pull request. GitHub creates tag `vX.Y.Z` and a Release.
4. The Windows job attaches `DataMan-X.Y.Z-win-x64.zip`.

Org owners must allow GitHub Actions to create pull requests. Settings > Actions > General > Workflow permissions.

## Security

Read `SECURITY.md`. Do not file a public issue for a vulnerability.
