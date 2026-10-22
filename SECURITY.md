# Security Policy

## Supported versions

Only the **latest release** of Simple Launcher receives security fixes. Releases are published on the
[Releases page](https://github.com/purelogiccode/SimpleLauncher/releases); older versions are not patched,
so please reproduce any issue on the newest release before reporting it.

## Reporting a vulnerability

**Please do not open a public issue, discussion or pull request for a security problem.**

Report it privately through GitHub's private vulnerability reporting, which is enabled for this repository:

<https://github.com/purelogiccode/SimpleLauncher/security/advisories/new>

The report stays visible only to you and the maintainers until an advisory is published. A useful report includes:

- the affected component (WPF app, Avalonia app, updater, packaging scripts, update/CDN endpoints, ...);
- the version (or commit) and platform (Windows x64/ARM64, Linux x64/ARM64);
- reproduction steps or a proof of concept, the impact you expect, and any suggested fix;
- whether the issue is already public anywhere else.

## What to expect

- **Acknowledgement:** within 3 business days.
- **Assessment:** a first triage and severity assessment within 7 business days.
- **Fixes:** coordinated with you; the advisory and a fixed release are published together when possible.
- **Credit:** you are credited in the advisory unless you ask to stay anonymous.

## Scope

In scope:

- the Simple Launcher applications (`SimpleLauncher`, `SimpleLauncher.Avalonia`), the updater
  (`SimpleLauncher.Updater`, `SimpleLauncher.Avalonia.Updater`) and the `SimpleLauncher.Core` library;
- the update/download chain (GitHub releases API, the `assets.purelogiccode.com` fallback, ZIP extraction);
- the build, packaging and release automation in `scripts/` and `.github/workflows/`;
- handling of user data, settings, logs and the support/bug-report endpoints.

Out of scope:

- vulnerabilities in third-party emulators, frontends, ROMs, BIOS files or metadata sources that Simple Launcher
  merely launches or downloads;
- issues that require an already-compromised machine, a modified binary, or a malicious `appsettings.json` supplied
  by the attacker;
- missing hardening headers or similar findings with no demonstrated impact.

## Safe harbor

We will not pursue legal action or ask GitHub to act against researchers who test this project in good faith,
stay within the scope above, avoid privacy violations, data destruction and service disruption, and give us a
reasonable amount of time to fix the issue before any public disclosure.
