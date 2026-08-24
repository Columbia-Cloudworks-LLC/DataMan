# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Release Please updates this file when a release pull request is merged to `main`.

## [0.3.0](https://github.com/Columbia-Cloudworks-LLC/DataMan/compare/v0.2.0...v0.3.0) (2026-08-24)


### Features

* **brand:** apply the primary palette and generated shell assets ([62e3fab](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/62e3fab23134dcd9fc4984e17904717df79a6b88))
* **brand:** draw the DataMan mark as a book polyline ([84b2467](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/84b2467b8bc17b9bc02ebff69c9b0867e5014b5f))
* **brand:** ship the mark, tiles, and primary accent ([188d2db](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/188d2db29a3b591f2b15df1f729c295e48371b5a))
* **core:** mirror parent_item_id as contains edges ([#64](https://github.com/Columbia-Cloudworks-LLC/DataMan/issues/64)) ([66389f1](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/66389f1815dbe6f83c32f47d2c8b21cd238b81cb))
* **desktop:** persist System, Light, and Dark appearance ([92ef457](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/92ef45791039a082ec042fbe747e0dd6e349a079))
* **desktop:** persist System, Light, and Dark appearance ([416a5d4](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/416a5d497fe4c87286c31168a34b19bd045dbb22))
* **desktop:** persist System, Light, and Dark appearance ([#62](https://github.com/Columbia-Cloudworks-LLC/DataMan/issues/62)) ([92ef457](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/92ef45791039a082ec042fbe747e0dd6e349a079))
* **desktop:** port dashboard, browser, settings, and brand chrome ([08bb1e4](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/08bb1e42b6810ca25ddb581f240db388a71bc763))
* **desktop:** scaffold Avalonia host with App DI and AppPaths ([a71705b](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/a71705b050c4533305ad6e8ec28ee6ddbdb76dc6))
* **desktop:** ship Avalonia Linux host for Ubuntu 24.04 ([538c058](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/538c05867cbfe4c3359a649876d4672ce4206e52))
* **desktop:** ship Avalonia Linux host for Ubuntu 24.04 ([#60](https://github.com/Columbia-Cloudworks-LLC/DataMan/issues/60)) ([538c058](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/538c05867cbfe4c3359a649876d4672ce4206e52))
* **embeddings:** extract OnnxTextEmbedder into DataMan.Embeddings ([1040388](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/1040388a58b9b60fcd5d96a2edb4ac30e256a90f))
* **library:** keep item identity when local files move ([7831b87](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/7831b87d76d31c5bf5cbd8f5690e287911d9961a))
* **library:** keep item identity when local files move ([79c0e49](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/79c0e49a9642a84e70524bfbbacc0cab2e983df7))
* **library:** persist watched folder roots across launch ([894c764](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/894c76406fbdcf6641f8c17c9911489b57854a55))
* **library:** resume watched folder roots on launch ([df911f4](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/df911f42b2d4a28bc8234eafb86663f4e43469f9))
* **plugins:** unload collectible plugin contexts ([b760476](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/b7604769409a471996a79e62f7bc15ec4c248d36))
* **plugins:** unload collectible plugin contexts ([39d5c38](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/39d5c3814702dd0656f7c69d667c4f8b77bb90c6))
* **search:** add Browser Text and Meaning search ([b31f0b3](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/b31f0b3cab6c425dcea0b14997d4982fb21a7dfc))
* **search:** add LibraryQuery semantic search ([8ee1563](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/8ee156309d5ff4f3a6eca1ea7beaef1839eb265a))
* **search:** index chunks after content commit ([4b95ceb](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/4b95ceb4bd7a5c4808333208da8045791262969b))
* **search:** load host MiniLM ONNX when present ([a438fc9](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/a438fc9910029f692d777cebbfc4dbfce5881b38))
* **search:** rank LibraryQuery.Semantic by nearest chunk ([238dbff](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/238dbfff2b3ed17cbfecdff27b3e37452f3c9686))
* **search:** replace Search(string) with LibraryQuery ([560e42d](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/560e42d1007928c76dabdf0fe706e5d2487c7a03))


### Bug Fixes

* **desktop:** serialize ingest entry and publish linux release ([398a9d9](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/398a9d987e726c5e68732baf9c2a523d10137169))
* **plugins:** drop the extra released flag ([052dfed](https://github.com/Columbia-Cloudworks-LLC/DataMan/commit/052dfedad92a6efe33e26c5c8413b5fcd1af8139))

## [0.2.0](https://github.com/Columbia-Cloudworks-LLC/DataMan/compare/v0.1.1...v0.2.0) (2026-08-24)


### Features

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
