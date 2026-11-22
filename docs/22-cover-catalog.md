# 22 - Cover Catalog (R2)

> Per-game cover delivery from the project's own collection, without any third-party
> thumbnail source. The app downloads only missing covers; the existing system ZIP packs
> remain available for full offline installs.

## R2 layout

Everything is stored in the existing `simplelauncher-thumbnails` bucket, under a dedicated
prefix so it cannot collide with the release assets or the old image packs:

```text
r2:simplelauncher-thumbnails/Simple Launcher/covers/v1/catalog.json
r2:simplelauncher-thumbnails/Simple Launcher/covers/v1/<system-slug>/index.json
r2:simplelauncher-thumbnails/Simple Launcher/covers/v1/<system-slug>/<canonical-name>.jpg
```

Public URLs:

```text
https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/catalog.json
https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/<system-slug>/index.json
https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/<system-slug>/<canonical-name>.jpg
```

Examples:

```text
https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/atari-5200/index.json
https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/atari-5200/BerZerk%20(USA).jpg
```

The old `Simple Launcher/<system>.zip` packs are not modified. The existing
`Microsoft Windows/` root prefix (store-game covers) is also left untouched; the retro
cover catalog excludes `Microsoft Windows` by default.

## Published catalog

The first full catalog was published on **2026-11-22**:

- **80 systems**
- **121,812 canonical covers**
- **121,893 R2 objects** (covers + 80 indexes + catalog)
- **7.108 GiB**
- `rclone check --size-only --one-way` reports **0 differences**

The public catalog is live at
`https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/catalog.json`.

## Filtering rules

The builder uses the same normalization as the app's local cover matcher:

1. `FindCoverImageService.StripAnnotations` removes `(...)`, `[...]` and `{...}` groups
   (including nested groups).
2. Images are grouped case-insensitively by the stripped name.
3. One canonical image is selected per group:
   - box art (portrait) first;
   - then the largest pixel area;
   - then PNG over JPEG/WebP;
   - then the unannotated name;
   - then the largest/shortest file.
4. Every removed variant name is stored as an alias in `index.json`. No extra image is
   uploaded for a region/crack/revision variant.
5. The app matches a ROM against the local image folder first, then the R2 index using the
   same annotation stripping and the existing Jaro-Winkler fuzzy matcher. A downloaded
   image is saved under the ROM's own base name, so the next launch is a local exact match.

The default build excludes `Microsoft Windows` (store covers already exist per-game under
`Microsoft Windows/`). Pass `-ExcludeSystem`/`--exclude-system` to change that. Non-image
files (ZIP/RAR archives) are counted as unsupported and are not uploaded. The two `.webp`
sources are supported by SkiaSharp and are re-encoded as JPEG.

## Derivatives

- Longest side: **800 px** (no upscaling).
- Format: JPEG quality **85**, flattened onto white when the source has alpha.
- This keeps a ~154k-cover set near **10 GB** instead of the ~50 GB originals.
- The source collection remains the master copy; only derivatives are uploaded.

## Build and upload

Build a staging folder first (nothing is uploaded):

```powershell
pwsh scripts/build-cover-catalog.ps1 `
  -Source "D:\Emulators\Simple Launcher\images" `
  -Output "D:\Temp\covers-stage"
```

Pilot one system:

```powershell
pwsh scripts/build-cover-catalog.ps1 `
  -Source "D:\Emulators\Simple Launcher\images" `
  -Output "D:\Temp\covers-stage" `
  -Systems "Atari 5200" `
  -Force
```

Review `<output>/report.md`, then dry-run the upload:

```powershell
pwsh scripts/publish-covers-r2.ps1 -StageDir "D:\Temp\covers-stage" -DryRun
```

Publish:

```powershell
pwsh scripts/publish-covers-r2.ps1 -StageDir "D:\Temp\covers-stage"
```

Upload order is deliberate:

1. images (`catalog.json`, `index.json` and `report.md` excluded);
2. per-system indexes;
3. `catalog.json` last.

The publisher uses `rclone copy`, never `sync --delete`, so existing R2 objects are never
removed. It verifies the public catalog, one index and one image with HEAD requests and
checks their `Content-Type`.

### Resuming an interrupted upload

The full catalog is over 120k small objects, so a slow connection can outlive a shell or CI
timeout. Resume with higher concurrency and skip objects that already exist:

```powershell
pwsh scripts/publish-covers-r2.ps1 `
  -StageDir "D:\Temp\covers-stage" `
  -IgnoreExisting -Transfers 48 -Checkers 96 -FastList
```

`-IgnoreExisting` is safe for a resume because a first full upload only adds objects; run a
final `-Transfers 16` pass without it if any source file changed between runs. The image
pass can be skipped entirely with `-SkipImages` after uploading images in batches.

## JSON contracts

`catalog.json`:

```json
{
  "version": 1,
  "baseUrl": "https://assets.purelogiccode.com/Simple%20Launcher/covers/v1",
  "generatedUtc": "2026-11-21T00:00:00Z",
  "systems": [
    { "name": "Atari 5200", "slug": "atari-5200", "count": 169, "indexFile": "atari-5200/index.json" }
  ]
}
```

`<slug>/index.json`:

```json
{
  "version": 1,
  "system": "Atari 5200",
  "slug": "atari-5200",
  "images": [
    {
      "file": "BerZerk (USA).jpg",
      "names": ["berzerk", "BerZerk (USA)"],
      "size": 61100,
      "width": 439,
      "height": 600
    }
  ]
}
```

The client should URL-encode each path segment (the remote names may contain spaces,
apostrophes, `#`, `$`, `%`, `?` and non-ASCII characters). The object key is stable; do not
rename uploaded objects when aliases change, only update the index.

## Privacy

Downloading a system index does not upload the library. Fetching an image reveals the
selected system and game name in the request path to the project's asset server (Cloudflare
R2), not to a third party. The rest of the library is not sent.

## Client integration

The Avalonia app exposes **Edit System → Download Missing Covers**. The flow is:

1. build one local name index from the selected system's image folder;
2. use the cached per-system `index.json` (24 h catalog TTL, 7 day index TTL, stale cache
   fallback when offline);
3. match exact aliases, then annotation-stripped names, then a normalized token order, then
   the existing Jaro-Winkler fuzzy matcher;
4. download only the matched canonical JPEG with 3 attempts and bounded parallelism;
5. save it as `<rom base name>.jpg` atomically and never replace an existing cover;
6. report `x / y`, current game, downloaded/already present/not found/failed and support
   cancellation through the loading overlay.

The cache lives in the app's per-user data folder (`%LocalAppData%\SimpleLauncher\CoverCatalog`
on Windows, the XDG data folder on Linux and the per-user support folder on macOS). The action
requires a selected system; otherwise it shows a status hint. No libretro or third-party
thumbnail repository is contacted at runtime.
