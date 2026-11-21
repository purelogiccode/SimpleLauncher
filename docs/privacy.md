# Privacy Policy

> **Last updated: 2026-11-20.** This policy describes the current build of Simple Launcher
> (the shipped Avalonia app and the WPF reference, including the bundled updater). It does not cover emulators,
> storefronts, RetroAchievements, or websites you open from the app - each has its own policy.
> The bundled tools have their own readmes; where they use the shared logging pipeline, the
> diagnostic-report section below applies to them too.

Simple Launcher is a local-first emulator frontend. It does not require an account, does not
show ads, and does not sell data. This page explains the small amount of data that leaves your
computer, why it is sent, and how to limit it.

## The short version

- Your library, ROM paths, favorites, play history, settings and logs stay on your device.
- The app sends a small **startup/launch statistics** ping and automatic **warning/error
  diagnostic reports**. Both are described below, with opt-out details.
- Features you choose - Easy Mode, cover-art lookups, RetroAchievements, the support form -
  send only the data needed for that feature.
- If you block network access, your local library still works; online features degrade.

## Data stored on your device

Simple Launcher stores its data in one SQLite database (`settings.dat`) plus log files. The
folder depends on the platform:

| Platform | Default data folder |
|---|---|
| Windows | `%LocalAppData%\SimpleLauncher` (or next to the exe in portable mode) |
| Linux | `~/.local/share/SimpleLauncher` |
| macOS | `~/Library/Application Support/SimpleLauncher` |

`settings.dat` contains your systems and ROM paths, favorites, play history, preferences, and
your RetroAchievements username, API key and password. Logs (`error_user.log`, `error.log`,
`critical_error.log`) are written locally; `error_user.log` rolls daily and is kept for 7 days.

RetroAchievements credentials are protected with Windows DPAPI on Windows. On Linux and macOS
no OS keychain is used: the values are stored Base64-encoded (obfuscation only, not
encryption). Protect your OS account and use disk encryption if this matters to you.

Simple Launcher may also write configuration files for the emulators you configure (for
example, a RetroAchievements login or controller settings). Those files stay on your device.

## Data sent automatically

### 1. Startup and emulator-launch statistics

On startup the app sends `applicationId = "simplelauncher"` and its version. When it launches
a game through an emulator, it sends the emulator's name (for example `RetroArch`). This is a
small first-party usage count used to decide which versions and emulators to support.

- It does **not** include an account, a device ID, your library, ROM names, play time, or
  anything that identifies you.
- The server necessarily sees your IP address and normal HTTP headers, as with any web request.
- There is no in-app switch for this ping in the current build. Block the endpoint at the
  network level (firewall, Pi-hole, or hosts file) if you do not want it.

### 2. Warning and error diagnostic reports

When the app logs a **warning or error**, the log event is submitted automatically to the
bug-report API so that defects can be found and fixed. A report contains:

- application name and version, date and time;
- operating system, architecture, 64/32-bit, processor count;
- your computer name, the application base directory, and the temp path;
- the log message and the exception type/message/stack trace.

Log messages and exception text can include file paths, system names, or game titles. The base
directory and temp path can include your Windows/Linux/macOS user name. Reports do **not**
include ROM/ISO/BIOS contents, save files, or your RetroAchievements password.

To disable automatic bug reports, set this environment variable before starting the app:

```text
SIMPLELAUNCHER_BUGREPORT_DISABLE=1
```

If the API is unreachable, the same report is written locally to `critical_error.log` instead.
Local logs are not uploaded unless a warning/error triggers a report.

### 3. Windows installer

The Windows installer checks for the .NET 11 runtime locally. If it is missing, it
downloads Microsoft's runtime installer from `aka.ms` / `builds.dotnet.microsoft.com` and runs it
with administrator approval. Microsoft sees your IP address and normal HTTP headers for that
download. Setup itself sends no telemetry to PureLogicCode. You can install the runtime yourself
from Microsoft and re-run Setup instead.

### 4. Update checks

The updater asks GitHub for the latest release, with a fallback to the project's asset server
when GitHub is unreachable. The request contains no account data; GitHub sees your IP address
and normal HTTP headers.

### 5. Website analytics (project site only)

The project's documentation and landing site can use **GoatCounter** or **Plausible**, both
cookie-free and privacy-friendly. If enabled, they record aggregate page views and download-click
events (platform, architecture and file format). They do not set cookies, store a persistent
identifier, fingerprint the browser, or build a profile of you; Do Not Track and Global Privacy
Control are honoured. The site works normally with analytics disabled. The app itself does not
use these website analytics. The documentation theme toggle stores only a local dark/light
preference in your browser; it is not sent anywhere.

## Data sent when you choose a feature

### Easy Mode

Easy Mode requests the system catalog for your platform (for example `linux-x64`) from the
PureLogicCode API. When you add a system, the emulator, core, and optional image pack are
downloaded from the project's asset server (Cloudflare R2). No library contents are sent.

### Store scans and cover art

Store scanning reads local manifests and registry entries on Windows. To tell games apart from
other installed apps, the names of Microsoft Store and EA app entries are sent to the
classification API. To fetch cover art for a store game, the game's name is sent to the cover
API; the returned image is downloaded from its URL. On Linux and macOS store scanning returns
no results, so nothing is sent.

### RetroAchievements

When you connect a RetroAchievements account, the app talks directly to
`retroachievements.org`: your username, API key/password, game hashes, and achievement progress
are sent to that service. Hashing runs locally - only the resulting hash is sent, not the ROM.
RetroAchievements' own privacy policy applies.

### Support requests

The in-app support form sends the name, email address, and message you type to the project's
support email service so that you can receive a reply. Support requests are user-initiated;
they are not sent unless you press Send.

### Links

Links to GitHub, the documentation, RetroAchievements, YouTube, IGDB, and other sites open in
your browser. Those sites receive normal browser requests under their own policies.

## What we do not collect

- No account registration, advertising ID, contact list, or precise location.
- No ROM, ISO, BIOS, save-state, or save-file upload.
- No selling, renting, or sharing of personal data for advertising.
- No third-party analytics or advertising SDK.

The startup/launch counts are used in aggregate. We do not use them to build a profile of you.

## Retention

- **Usage counts:** aggregate counters; no per-user history is maintained by the app.
- **Bug reports and support messages:** kept only as long as needed to investigate and respond,
  then deleted unless retention is required for security or legal reasons.
- **Local logs:** kept on your device for 7 days by the app; delete the data folder to remove
  them sooner.

## Security

Network requests use TLS 1.2/1.3. Local credentials use the protection described above. No
software can guarantee absolute security; keep your OS account and disk protected, and download
releases only from the official GitHub repository.

## Your choices

- **Block telemetry or reports:** set `SIMPLELAUNCHER_BUGREPORT_DISABLE=1`, or block the
  PureLogicCode endpoints in your firewall/hosts file. The app remains usable offline.
- **Avoid online features:** do not use Easy Mode, store scanning, cover lookups,
  RetroAchievements, or the support form.
- **Delete local data:** close the app and delete the data folder listed above. This removes
  settings, library metadata, logs, and saved credentials.
- **Request deletion of a report or support message:** email
  [contact@purelogiccode.com](mailto:contact@purelogiccode.com) with the approximate date and
  the application version. Anonymous aggregate counters cannot be tied back to a person.
- **RetroAchievements:** manage or delete your account at
  [retroachievements.org](https://retroachievements.org/).

## Children

Simple Launcher is not directed at children under 13. We do not knowingly collect personal
information from children. If you believe a child sent us personal information, contact us and
we will delete it.

## Changes

We will update this page when the app's data practices change and revise the date at the top.
Material changes will also be noted in the release notes.

## Contact

Questions or requests: [contact@purelogiccode.com](mailto:contact@purelogiccode.com).

## Third-party services

| Service | Used for | Data involved |
|---|---|---|
| [GitHub](https://github.com/purelogiccode/SimpleLauncher) | Releases, update checks, issue tracker | IP address and normal HTTP headers |
| [Microsoft](https://dotnet.microsoft.com/) | .NET 11 runtime download when the Windows installer needs it | IP address and normal HTTP headers |
| [PureLogicCode](https://www.purelogiccode.com) (stats, bug reports, Easy Mode, support email) | First-party APIs described above | See the sections above |
| [Cloudflare](https://www.cloudflare.com/privacypolicy/) (R2 assets, cover API host) | Emulator/image downloads, cover lookups | IP address; game name for cover lookups |
| [jsDelivr](https://www.jsdelivr.com/) (docs page) | Loads the docsify renderer and theme | IP address and normal HTTP headers |
| [RetroAchievements](https://retroachievements.org/) | Achievement profiles, hashes, progress | RetroAchievements account and game hashes |
| [GoatCounter](https://www.goatcounter.com/) or [Plausible](https://plausible.io/) (project site only, if enabled) | Aggregate page views and download clicks | Page/event path, platform, architecture, format; no cookies or personal identifiers |
| Emulators and storefronts you use | Running games, store libraries | Governed by their own policies |

---

*Simple Launcher is free software under the GPLv3. This policy describes the app, not the games
or emulators you run with it.*
