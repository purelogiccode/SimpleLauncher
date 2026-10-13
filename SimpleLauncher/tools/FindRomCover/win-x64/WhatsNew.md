# FindRomCover 3.3.0 — What's New

## Cross-Platform Release (Avalonia)

- **Windows, Linux, and macOS** — the application was migrated from WPF to **Avalonia 12.1.4** and now runs on all three operating systems from a single codebase, with the same themes, accent colors, and dark/light modes.
- **Native web views** — the embedded browser uses WebView2 on Windows, WKWebView on macOS, and WebKitGTK on Linux. When the engine is missing, the affected tab falls back to the system browser and a one-time prompt links to the correct runtime.
- **Per-OS services** — click sounds, screenshots, and data folders all use platform-appropriate implementations (`%LocalAppData%\FindRomCover` on Windows, `~/.local/share/FindRomCover` on Linux, `~/Library/Application Support/FindRomCover` on macOS).

## Fixes & Improvements

- **Covers land on the right game** — recycled context menus now refresh their target, so "Use This Image" can never save a cover to the previously selected ROM.
- **AI query history is protected** — a healthy `QueryHistory.dat` is no longer quarantined when schema creation fails (read-only file, disk full); only real SQLite corruption triggers a fresh database.
- **Quieter bug reports** — network hiccups, invalid API keys, quota limits, corrupt user images, and other environmental failures are logged locally instead of filing automatic bug reports; genuine failures still report exactly once.
- **Small lifecycle fixes** — the progress spinner resumes after tab switches, refreshed watcher registrations keep their full 60-second window, and invalid folder paths also cancel in-flight AI picks.

## Under the Hood

- **879 passing tests** across 68 files; the legacy WPF project is kept in `FindRomCover_Legacy` and is no longer built.
- Service interfaces moved to `Interfaces/`, models/enums to `Models/`; asynchronous methods use the `Async` suffix throughout.
- Version bumped to **3.3.0**.

## Download

- `release_3.3.0_win-x64.zip` — Windows x64
- `release_3.3.0_win-arm64.zip` — Windows ARM64
- `release_3.3.0_linux-x64.zip` — Linux x64
- `release_3.3.0_linux-arm64.zip` — Linux ARM64
- `release_3.3.0_osx-arm64.zip` — macOS Apple Silicon

> **Note:** there is no Intel-Mac build: Magick.NET 15 (the image conversion engine) no longer ships a macOS x64 native library. Replacing it with a fully cross-platform engine is planned for a future release.

All builds are framework-dependent and require the [.NET 10.0 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (the **Desktop Runtime** on Windows). Extract and run `FindRomCover.exe` on Windows or `FindRomCover` on Linux/macOS. Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`.

---

# FindRomCover 3.2.2 — What's New

- **Settings menu cleanup** — `Ignore Bracketed Text in Matching` now lives under the **Settings** menu instead of the top level, grouping it with the other matching and display toggles.

## Download

- `release_3.2.2_win-x64.zip` — Windows x64
- `release_3.2.2_win-arm64.zip` — Windows ARM64

Both builds are framework-dependent and require the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Extract and run `FindRomCover.exe`.

---

# FindRomCover 3.2.1 — What's New

- **New default OpenRouter model** — the OpenRouter provider now defaults to `google/gemma-4-26b-a4b-it`, which follows the cover/box-art instructions more reliably than the previous default. Existing installations keep their configured model; the new default applies to fresh setups and whenever the Model field is left empty.

## Download

- `release_3.2.1_win-x64.zip` — Windows x64
- `release_3.2.1_win-arm64.zip` — Windows ARM64

Both builds are framework-dependent and require the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Extract and run `FindRomCover.exe`.

---

# FindRomCover 3.2.0 — What's New

## AI Vision Assist (New)

- **AI Pick Best** on the Local Files and Google API tabs: a vision-capable model looks at the candidate images and highlights the best cover (green badge, moved to the front).
- **Cloud or local models**: OpenRouter, OpenAI, Anthropic (Claude), Google Gemini and GLM (Zhipu/Z.AI), plus local OpenAI-compatible servers (Ollama, LM Studio) and custom OpenAI/Anthropic-compatible endpoints.
- **Default OpenRouter model** is now `qwen/qwen3.7-flash`, the cheapest vision-capable model on OpenRouter (roughly $0.21 per 1,000 cover picks).
- **AI Settings** window: provider presets, encrypted API key storage, an editable model picker with **Test / Load Models**, request timeout, candidate/image limits, auto-save threshold, auto-run and verify-on-save toggles.
- **Smart model picker**: the loaded model list is filtered to vision-capable models by default (using provider modality metadata where available and a model-name heuristic otherwise), searchable with a filter box, and cached per provider for 7 days so it is available instantly next time.
- **Verify before saving**: the folder watcher asks the model to confirm that a newly saved image matches the selected game before renaming it. Mismatches are left untouched and reported instead of being silently renamed.
- **AI Batch Fill**: process the whole missing-covers list — local candidates first, optional Google API fallback — saving only when the confidence is above the threshold, with live progress, cancel and per-item results.
- **AI candidate similarity threshold**: the existing filename matcher now pre-filters candidates — only local images whose filename similarity is at or above the configured value (default 70%) are sent to the AI, saving tokens and reducing noise.
- **No duplicate queries**: missing covers that were already queried (manually or by batch fill) are remembered across sessions in the SQLite database `QueryHistory.dat`, so the AI is not asked twice for the same cover. Batch Fill skips them, and the history can be reviewed and cleared in AI Settings.
- Images are downscaled before upload (default 512px) and verdicts are cached for 30 days to keep API usage low.
- **Minimum cover size** — downloads narrower than the configured **Min cover width** (default 200px) are rejected, so tiny thumbnails never become covers.
- **Cover-first API picks** — when ranking Google API results, the model is told to prefer real cover art or an in-game screenshot over a photo of a physical cartridge, cart, or disc.
- **Fixed empty AI responses**: reasoning models (GPT-5 Nano, Qwen3.7 Flash, Gemini thinking models) could spend the entire 400-token output limit on internal reasoning and return no text; the limit is now 4,096 tokens, and a clear error explains the cause if a model still runs out.
- **More reliable batch downloads**: images are requested with browser-like headers, and when a source blocks the full-size Google API image, the provider-hosted thumbnail is used as a fallback instead of failing.
- **Accurate already-filled detection**: cover detection now recognizes every supported image format and sanitized filenames, so the missing list no longer keeps games whose covers are already on disk; Batch Fill reports them once and removes them, and the list refreshes after every run.

## Fixes & Improvements

- **Ignore bracketed text when matching** — a new `Ignore Bracketed Text in Matching` menu option (enabled by default) removes balanced `(...)`, `[...]`, and `{...}` groups from ROM and image names before scoring, so `Game (USA) [En]` matches a cover named `Game`. Works with all three similarity algorithms and with AI candidate pre-filtering.
- **Image download failures no longer trigger bug reports** — a dead or unreachable image host (connection timeout, refused connection, reset) is now logged as a warning and the thumbnail fallback is still tried, instead of aborting the download and filing an automatic bug report ([#67379](https://github.com/purelogiccode/FindRomCover/issues/67379), [#67382](https://github.com/purelogiccode/FindRomCover/issues/67382), [#67383](https://github.com/purelogiccode/FindRomCover/issues/67383), [#67432](https://github.com/purelogiccode/FindRomCover/issues/67432)).
- **"File is being used by another process" when saving covers** — each save attempt now writes to a uniquely named temp file and write failures after all retries return a clear error dialog instead of an unhandled exception, fixing collisions from double-clicks, AI auto-save racing a manual save, and antivirus/sync locks ([#67436](https://github.com/purelogiccode/FindRomCover/issues/67436), [#67437](https://github.com/purelogiccode/FindRomCover/issues/67437), [#67438](https://github.com/purelogiccode/FindRomCover/issues/67438)).
- **Save failure details in the UI** — when a cover cannot be saved, the dialog and log now include the real reason (locked file, permissions) instead of a generic "Failed to save the image." message.
- **Watched folders recover automatically** — a folder that becomes temporarily unreadable (permissions, disconnected drive, antivirus) is logged as a warning, the watcher retries with backoff, and it no longer files automatic bug reports ([#66867](https://github.com/purelogiccode/FindRomCover/issues/66867)).
- **Missing-list selection is kept** — removing an item keeps the selection on the nearest entry instead of resetting to the top.
- **Anthropic and Gemini providers now actually work** — the provider adapters were never selected at runtime, so those requests went to an OpenAI-compatible endpoint and failed. Picks and verification now use the correct native adapter per provider.
- **Covers are protected from accidental replacement** — Google API downloads are verified before they replace an existing cover, the folder watcher no longer overwrites an existing PNG, and startup cleanup only removes the app's own temp files instead of any file containing `.tmp`.
- **Picks never land on the wrong game** — verification, saving, and missing-list removal use the ROM that was selected when the action started, even if the selection changes while the AI is still working.
- **Google API timeouts handled properly** — a timeout now shows a real error instead of "Search canceled." with a stuck progress state, and no longer aborts an entire AI batch fill.
- **OpenAI reasoning models fixed** — `gpt-5-nano` and o-series models are called with `max_completion_tokens` and without `temperature`, which the OpenAI API requires.
- **Large missing lists no longer freeze the AI Batch window** — query-history status is loaded in a single query instead of one database open per item.
- **Sturdier settings storage** — an unreadable settings database is preserved as `Settings.dat.corrupt` instead of being overwritten with defaults, and legacy settings are kept until the SQLite save succeeds.

## Under the Hood

- New `IVisionModelClient` abstraction with native Anthropic Messages API and Gemini `generateContent` adapters; OpenAI, GLM, local and custom OpenAI-compatible endpoints share one adapter, while custom Anthropic-compatible endpoints reuse the Anthropic adapter. Shared prompts, JSON parsing, timeouts and friendly error messages.
- **Settings moved to SQLite**: all settings (including API keys, encrypted) now live in `%LocalAppData%\FindRomCover\Settings.dat`. The old encrypted `settings.dat` is migrated automatically on first start and kept as `settings.dat.legacy`.
- **Full documentation**: a professional documentation site (GitHub Pages) and a synchronized GitHub wiki with side navigation, including release notes and recommended AI models. CI publishes both.
- **Dependency updates**: Magick.NET, NAudio, Microsoft.Extensions.DependencyInjection, the Roslyn/Meziantou analyzers and the .NET test SDK were updated.
- **Log writes are serialized**: the error logger clears and appends under one lock, so entries cannot be lost or truncated during concurrent reporting.
- AI request failures are logged as warnings and never trigger the automatic bug-report pipeline.
- Version bumped to **3.2.0**.
- GitHub Actions CI now builds and tests every push and pull request on Windows.

## Download

- `release_3.2.0_win-x64.zip` — Windows x64
- `release_3.2.0_win-arm64.zip` — Windows ARM64

Both builds are framework-dependent and require the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Extract and run `FindRomCover.exe`.

---

# FindRomCover 3.1.0 — What's New

## Fixes & Improvements

- **Automatic scan after selecting a folder** — Browsing to your ROM or Image folder now scans automatically as soon as both folders are set. No more wondering why the list stays empty after using "Browse..." ([#1](https://github.com/purelogiccode/FindRomCover/issues/1))
- **Clear scan feedback** — The status bar now reports how many ROM files matched the supported extensions and how many are missing covers. If nothing matches, you get an explicit message instead of a silently empty list. ([#1](https://github.com/purelogiccode/FindRomCover/issues/1))
- **Refresh after editing supported extensions** — Changing the extension list (Settings > Edit Supported Extensions...) now re-scans immediately instead of showing stale results.
- **Repository migrated** — The project now lives at [purelogiccode/FindRomCover](https://github.com/purelogiccode/FindRomCover). The built-in update checker follows the new releases page.

## Under the Hood

- Hardened settings loading: a corrupt `settings.dat` is quarantined as `settings.dat.corrupt` and defaults are restored instead of failing.
- Added a managed PBKDF2 fallback so settings decryption no longer crashes on systems where the OS cryptographic implementation fails.
- Friendlier Google Custom Search API error messages (invalid API key, quota exceeded, bad requests) instead of raw JSON dumps.
- More robust link opening with an `explorer.exe` fallback for systems without a default browser association.
- Version bumped to **3.1.0**.

## Download

- `release_3.1.0_win-x64.zip` — Windows x64
- `release_3.1.0_win-arm64.zip` — Windows ARM64

Both builds are framework-dependent and require the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Extract and run `FindRomCover.exe`.
