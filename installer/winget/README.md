# winget package — `PureLogicCode.SimpleLauncher`

The winget package installs the **per-user Inno Setup installer**
(`SimpleLauncher-{version}-win-{x64,arm64}-setup.exe`), not the portable ZIP.

| Field | Value |
|---|---|
| Package identifier | `PureLogicCode.SimpleLauncher` |
| Installer type | `inno` |
| Scope | `user` (`%LocalAppData%\SimpleLauncher`) |
| Silent switches | `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-` |
| Dependency | `Microsoft.DotNet.Runtime.11` (the payload is framework-dependent) |
| ARP product code | `{2016DA44-0694-42F7-92A3-4015CF9069A5}_is1` (Inno `AppId_is1`) |

The dependency installs the .NET 11 runtime silently before Setup runs. The installer
can also download the runtime itself, but that path needs a UAC prompt, which would hang an
unattended `winget install`; the dependency avoids it. If the Windows payload ever becomes
self-contained, remove the dependency from the generated installer manifest.

## Generate the manifests

1. Build the installers for the release version (both RIDs):

   ```powershell
   pwsh scripts/package-release.ps1 -Version <version> -BuildInstaller
   ```

2. Make sure the matching GitHub release (tag `release<version>`) has both
   `SimpleLauncher-<version>-win-x64-setup.exe` and `...-win-arm64-setup.exe` published.
   Then generate and validate the manifests:

   ```powershell
   pwsh scripts/update-winget-manifests.ps1 -Version <version> -VerifyUrls
   ```

   The script writes the three manifests in the winget-pkgs layout:

   ```text
   artifacts\winget\manifests\p\PureLogicCode\SimpleLauncher\<version>\
     PureLogicCode.SimpleLauncher.yaml
     PureLogicCode.SimpleLauncher.installer.yaml
     PureLogicCode.SimpleLauncher.locale.en-US.yaml
   ```

   It hashes the setup executables that are in `artifacts\release` (or `-SetupDir`). Rebuilding
   an installer changes its hash, so always regenerate after a rebuild and submit the same
   binaries that were uploaded to the release.

## Validate and test

`winget validate` runs automatically; to run it on its own:

```powershell
winget validate --manifest artifacts\winget\manifests\p\PureLogicCode\SimpleLauncher\<version>
```

Local validation reports the `.NET` dependency as "not validated" because it is resolved from the
remote winget source; that warning is expected. The generated `manifests\` tree is under
`artifacts\` (gitignored) and is meant to be copied into the `microsoft/winget-pkgs` fork.

Test the actual install from the local manifests on a clean Windows VM:

```powershell
winget install --manifest artifacts\winget\manifests\p\PureLogicCode\SimpleLauncher\<version>
winget list --id PureLogicCode.SimpleLauncher
winget upgrade --id PureLogicCode.SimpleLauncher   # after publishing the next version
winget uninstall --id PureLogicCode.SimpleLauncher
```

Acceptance checks:

- The package installs without an interactive prompt and without a UAC prompt for the app
  itself (the runtime dependency may elevate once when it is missing).
- The app lands in `%LocalAppData%\SimpleLauncher`, with both Start Menu shortcuts.
- `winget list` shows `PureLogicCode.SimpleLauncher` and the installed version.
- Uninstalling removes the program files and keeps `settings.dat`, `system.xml`, logs and game
  media, matching the installer's data-preserving behaviour.
- On ARM64, winget selects the arm64 installer.

## Submit to `microsoft/winget-pkgs`

Either use WingetCreate:

```powershell
winget install --id Microsoft.WingetCreate -e
wingetcreate submit --prtitle "New package: PureLogicCode.SimpleLauncher version <version>" `
  artifacts\winget\manifests\p\PureLogicCode\SimpleLauncher\<version>\PureLogicCode.SimpleLauncher.yaml
```

Or open the PR manually: fork `microsoft/winget-pkgs`, copy the generated `manifests` tree into
the fork root (the path must become
`manifests/p/PureLogicCode/SimpleLauncher/<version>/`), commit, and open a PR. The first
submission also goes through the Microsoft CLA bot.

After the package is live, add the command to user-facing docs:

```powershell
winget install --id PureLogicCode.SimpleLauncher -e
```

## Future versions

Every release needs a new `<version>` folder with updated `PackageVersion`, URLs, hashes and
`ReleaseNotesUrl`. The generator handles all of it; do not copy an old folder by hand. Keep the
existing version folders in mind when using `-r/--replace` with WingetCreate — only use replace
when fixing the current submission.
