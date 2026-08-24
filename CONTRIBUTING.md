# How to contribute

This repository is the Windows x64 host. Other platforms are out of scope.

## Build

Install the .NET 8 SDK and the C# / C# Dev Kit extensions in Cursor.

```powershell
.\scripts\dev-build.ps1
.\scripts\verify-ship.ps1
```

`Terminal > Run Task > build` and `Debug DataMan` use the same unpackaged x64 exe.
`Terminal > Run Task > test` runs `verify-ship.ps1`.

## Pull requests

1. Branch from `main`.
2. Keep the change one concern.
3. Use [Conventional Commits](https://www.conventionalcommits.org/) in the commit subject.
4. Run `.\scripts\verify-ship.ps1` before you open the pull request.

Subjects look like `feat(ingest): keep folder items on drop` or `fix(search): escape FTS tokens`.
`feat` and `fix` are what Release Please uses to open the next version pull request.

## Release

Do not tag by hand for a normal ship.

1. Merge work to `main`. The `test` check must stay green.
2. Release Please opens or updates a release pull request. It rewrites `CHANGELOG.md` and `Directory.Build.props`.
3. Merge that pull request. GitHub creates tag `vX.Y.Z` and a Release.
4. The Windows job attaches `DataMan-X.Y.Z-win-x64.zip`.

Release Please needs the organization to let GitHub Actions create pull requests. An org owner opens Organization settings > Actions > General > Workflow permissions. Set Read and write permissions. Check Allow GitHub Actions to create and approve pull requests.

Until that box is on, open the version pull request by hand from `release-please--branches--main` after the release workflow updates that branch.

## Graphite

Graphite Hobby is free for personal repositories only. This repository is owned by the Columbia Cloudworks LLC organization, so a Graphite workspace here is a 30-day trial or a paid Starter seat. Do not install the Graphite GitHub App on the org unless you accept that.

The `gt` CLI can still sit on a local PATH. `gt init --trunk main` writes `.git/.graphite_repo_config`. That file is local. To authenticate later, open [Graphite activate](https://app.graphite.com/activate) and run the `gt auth --token` command it shows.

Ship with `gh pr merge` until Graphite is actually on the org. Do not enable GitHub auto-merge on stacked pull requests.

## Security

Read `SECURITY.md`. Do not file a public issue for a vulnerability.
