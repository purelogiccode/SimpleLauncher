# 13 — Logging & Debug

> Serilog setup, sinks, the Debug Window, and bug-reporting.
> Related: [04 — Architecture](04-architecture.md) · [08 — UI Layer](08-ui-layer.md)

## Serilog bootstrap

Serilog 4.4.0 (+ `Serilog.Sinks.Async` 2.1.0, `Serilog.Sinks.Debug` 3.0.0, `Serilog.Sinks.File` 7.0.0) is configured in `App.OnStartup` (`App.xaml.cs:117-137`):

- **File sink:** rolling daily file in the local app-data folder (`%LocalAppData%\SimpleLauncher\error_user.log` by default, or `LogPath` override), 7 days retained.
- **`DebugWindowSink`:** pushes log events to the live Debug Window (buffered; flushed when the window connects).
- **`BugReportApiSink`:** queues events for the bug-report API (see below).
- **Fallback:** if Serilog initialization fails, the app falls back to debug output instead of crashing.

`ILogger` (Serilog) is injected everywhere — Core and app projects both have a global `using Serilog;` (see [02 — Projects & Solution](02-projects-and-solution.md)).

## Debug Window

- `DebugWindow` (app): singleton live log viewer; **closing hides the window** instead of destroying it; logging continues; reopening flushes the buffered history.
- Launched via the tray menu / Help menu, or automatically with the **`-debug` command-line flag** (opens alongside the main window).
- `IDebugLogger` (legacy abstraction) and `NoOpDebugLogger` (fallback) still exist for compatibility; new code uses Serilog `ILogger` directly.

## Bug report pipeline

`BugReportApiSink` (`SimpleLauncher.Core\Services\DebugAndBugReport\BugReportApiSink.cs`):

1. Log events are queued (bounded — warnings do not grow unbounded, ~100-cap) and submitted to the bug-report API.
2. On **success**: queued logs are deleted.
3. On **failure**: `critical_error.log` (or `error.log` / `error_user.log`) is written locally with environment details; `WindowsVersionService`/`GetMicrosoftWindowsVersion` supply OS info for reports.

The **Support Window** (`SupportWindow` + `SupportViewModel`) is the user-facing entry point: name/email/message validation, "Sending support request…" overlay, POST to the support API, and an emergency return button on the overlay.

### Expected conditions (Information level)

Project policy: expected environment/user conditions are logged at **Information**, never Warning/Error, so they are not forwarded to the bug-report API. Current examples:

- **Audio** — missing `libsndfile`/`libasound`, no usable audio device, or a corrupt/unsupported sound file (`PlaySoundEffects.IsExpectedPlaybackFailure`; MP3/WAV use managed decoders, so the built-in sounds work without native packages).
- **Mounting** — mounting Windows-only disc/archive images ("not supported on this platform") on Linux/macOS.
- **Launching** — invalid/unlaunchable executables (Windows codes 193/216, Unix errno 8/13) and user-canceled elevation prompts.
- **Updates** — GitHub 403/429, download timeouts after retry, updater "process not found", a GitHub release without assets for the current platform (LB-23).
- **Settings database** — a `settings.dat` written by a newer app build (downgrade): migration and saves fail safe at Information, leaving the database and the legacy files untouched (`NewerSchemaVersionException`).
- **Files** — missing files on launch/delete.

## Log locations summary

| Path | Content |
|---|---|
| `%LocalAppData%\SimpleLauncher\error_user.log` | rolling daily log (7 days) |
| `%LocalAppData%\SimpleLauncher\error.log` | error-level log |
| `%LocalAppData%\SimpleLauncher\critical_error.log` | written when the bug-report API is unreachable |
| Debug Window (in-app) | live stream via `DebugWindowSink` |

## Related docs

- [04 — Architecture](04-architecture.md) (startup sequence)
- [08 — UI Layer](08-ui-layer.md) (Debug Window, status bar)
- [14 — Testing](14-testing.md) (log-related tests)
