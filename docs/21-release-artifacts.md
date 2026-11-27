# 21 - Release Artifact Matrix (Planned for the Next Cycle)

> **Status: planned only - not implemented.** The `.deb` and `.rpm` rows below are the target
> for the next release cycle. Do not add them to the release scripts, workflows, R2 publisher,
> landing page or updater expectations until the implementation checklist at the end of this
> page is complete.

This page records the agreed download matrix so the packaging work can be scheduled without
changing the current release. Existing artifacts keep their current names and behavior.

## Target matrix

| Platform | Format | Architectures | Proposed file | Runtime | Update path |
|---|---|---|---|---|---|
| Windows | Portable ZIP | x64, ARM64 | `release_{version}_win-{x64,arm64}.zip` | .NET 11 runtime required | In-app updater |
| Windows | Installer | x64, ARM64 | `SimpleLauncher-{version}-win-{x64,arm64}-setup.exe` | .NET 11 bootstrap | Install newer setup / in-app updater |
| macOS | DMG | Apple Silicon (arm64) | `SimpleLauncher-{version}-osx-arm64.dmg` | Bundled | Install the next DMG manually |
| Debian / Ubuntu / Linux Mint | Debian package | amd64, arm64 | `simplelauncher_{version}_amd64.deb`<br>`simplelauncher_{version}_arm64.deb` | Bundled preferred (decision below) | `apt` / package manager |
| Fedora / RHEL / openSUSE | RPM package | x86_64, aarch64 | `simplelauncher-{version}-1.x86_64.rpm`<br>`simplelauncher-{version}-1.aarch64.rpm` | Bundled preferred (decision below) | `dnf` / `zypper` |
| Any Linux | AppImage | x64, ARM64 | `SimpleLauncher-{version}-linux-{x64,arm64}.AppImage` | Bundled | In-app self-replace |
| Any Linux | Flatpak | x64, ARM64 | `SimpleLauncher-{version}-linux-{x64,arm64}.flatpak` | `org.freedesktop.Platform//25.08` | Software center / reinstall bundle |
| Any Linux | Portable tarball | x64, ARM64 | `release_{version}_linux-{x64,arm64}.tar.gz` | .NET 11 runtime required | In-app updater |

Package-manager formats do **not** use the in-app updater: updates come from the repository that
installed them. This must be reflected in `PackagingEnvironment` before the first `.deb`/`.rpm`
release (see below).

Architecture mapping:

| Release RID | Debian arch | RPM arch | Windows name |
|---|---|---|---|
| `linux-x64` | `amd64` | `x86_64` | `x64` |
| `linux-arm64` | `arm64` | `aarch64` | `arm64` |
| `win-x64` | - | - | `x64` |
| `win-arm64` | - | - | `arm64` |
| `osx-arm64` | - | - | `arm64` |

## Decisions to confirm before implementation

1. **Runtime strategy for `.deb`/`.rpm`.** Recommended: ship the self-contained payload, like
   AppImage and Flatpak, so installation does not depend on a Microsoft .NET feed or a distro
   package that may not have .NET 11. The alternative is a package dependency on the Microsoft
   .NET 11 runtime; that is smaller but adds an external repository requirement on Debian/Ubuntu
   and different package names per distro.
2. **Install layout.** Recommended: payload in `/opt/simplelauncher`, launcher at
   `/usr/bin/simplelauncher`, desktop file at
   `/usr/share/applications/com.purelogiccode.SimpleLauncher.desktop`, icons under
   `/usr/share/icons/hicolor/`, license in `/usr/share/doc/simplelauncher/`.
3. **User data.** Keep settings, logs, play history and downloaded media in
   `~/.local/share/SimpleLauncher` (the existing portable location). Uninstall must never delete
   that folder; the package docs must say so.
4. **Dependencies.** `libasound2` for audio; `libfuse3` optional for direct ZIP/CHD/XISO mounts;
   `libsndfile1` optional for FLAC/Ogg/Opus sounds. Use package-manager dependencies rather than
   silently failing at runtime.
5. **Package version.** Keep the app version and package release number aligned
   (`{version}-1` for RPM; `{version}` for DEB). Version bumping remains in
   `SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj` plus the synchronized files enforced by
   `VersionConsistencyTests`.
6. **Final file names.** The names above are the proposed convention. If a different prefix
   (`SimpleLauncher-...`) is preferred, decide it once and update the release workflow, R2
   publisher, checksums, landing page and docs together.

## Packaging plan

- Build the existing Linux payload once per RID (`linux-x64`, `linux-arm64`); do not duplicate
  the .NET publish per package format.
- Create the DEB control metadata (`Package: simplelauncher`, `Architecture: amd64/arm64`,
  `Maintainer`, `Section: games`, `Priority: optional`, `Homepage`, `Description`) and stage the
  payload under `/opt/simplelauncher`.
- Create the RPM spec (or use `fpm`) with the equivalent metadata, `%install`, `%files`,
  `%post`/`%postun` desktop/icon cache refreshes and no user-data deletion in `%preun`/`%postun`.
- Build on the Ubuntu packaging runner with `dpkg-deb --build` and `rpmbuild` (or `fpm`).
  Cross-architecture packaging is allowed: use the RID-specific payload and set the declared
  package architecture. Runtime smoke tests still need the matching architecture (native ARM64
  host or QEMU/container).
- Extend `PackagingEnvironment` so `.deb`/`.rpm` installs report:
  - `HasReadOnlyInstall = true` (the payload is owned by the package manager);
  - `UpdatesManagedExternally = true` (no in-app updater);
  - a package-manager-specific update message instead of "download a new ZIP".
- Add `.deb`/`.rpm` build and verification steps to both
  `.github/workflows/release.yml` and `.github/workflows/package-dry-run.yml`.
- Add the new assets to `SHA256SUMS.txt`, the R2 fallback publisher
  (`scripts/publish-fallback-r2.ps1`) and the release asset verification list.
- Update the landing page OS detection with Debian/Ubuntu/Mint and Fedora/RHEL/openSUSE hints.
  Keep AppImage as the "any Linux, no install" recommendation and the direct `.deb`/`.rpm`
  downloads as distro-specific options.
- Update `docs/19-linux-packages.md` to describe install, upgrade and uninstall commands, and
  the fact that system packages do not use the in-app updater.

## Acceptance checklist (next cycle)

- [ ] Debian package builds for `amd64` and `arm64`.
- [ ] RPM package builds for `x86_64` and `aarch64`.
- [ ] Clean install works on Debian 12, Ubuntu 24.04/Linux Mint 22, Fedora current, RHEL 9 and
      openSUSE Leap (at least one representative per package family).
- [ ] Upgrade over the previous package version works.
- [ ] Uninstall removes program files but keeps `~/.local/share/SimpleLauncher`.
- [ ] Desktop entry, icon and `simplelauncher` command are present after install.
- [ ] In-app update check is disabled for package-managed installs.
- [ ] Release workflow, dry-run workflow, `SHA256SUMS.txt` and R2 publishing include all new
      assets.
- [ ] Landing page and `docs/19-linux-packages.md` no longer describe `.deb`/`.rpm` as planned.
- [ ] ARM64 packages have been launched on real ARM64 hardware (or the existing ARM64 test
      path) before the release is published.

Until this checklist is complete, keep the current release matrix unchanged: Windows ZIP +
setup, macOS arm64 DMG, Linux tar.gz + ZIP + AppImage + Flatpak.
