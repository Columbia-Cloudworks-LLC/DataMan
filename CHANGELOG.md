# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Release Please updates this file when a release pull request is merged to `main`.

## [0.2.0](https://github.com/Columbia-Cloudworks-LLC/DataMan/compare/v0.1.1...v0.2.0) (2026-08-24)


### Features

* **plugins:** load contracts-only assemblies from a plugins directory ([beada5f](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/beada5f7dc7012cfbc5bf0f1fc5ffbe353d535b7))
* **plugins:** load contracts-only assemblies from a plugins directory ([9c75eb0](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/9c75eb02d94992480ef7ad8ee5b13e01824b0fce))


### Bug Fixes

* **plugins:** activate only plugins resolved from acyclic bundles ([ff31dd3](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/ff31dd3bf95d08ea9d51c9442b8aaa0f109b7415))
* **plugins:** keep built-ins when a plugin manifest is unreadable ([550486c](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/550486ca9946f47865581e6176dcb8a81212aa51))

## [0.1.1](https://github.com/Columbia-Cloudworks-LLC/DataMan/compare/v0.1.0...v0.1.1) (2026-08-24)


### Bug Fixes

* **ci:** isolate the publish zip path from dotnet logs ([cb3777d](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/cb3777d1b38f6adf8a1ae030abaf7b9f0710fe35))

## [0.1.0] - 2026-08-23

### Added

- Phase 0 and Phase 1 host. Ingest `.txt`, `.md`, and `.log` files into a local SQLite library.
- FTS5 search, Data Browser, and Dashboard drag-and-drop.
- Cursor workspace tasks, debug launch, and an after-edit format-and-build hook.
- Windows x64 CI and a self-contained zip publish path.
