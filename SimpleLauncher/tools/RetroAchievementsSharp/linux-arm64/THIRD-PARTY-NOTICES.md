# Third-Party Notices

RetroAchievementsSharp is a GPL-2.0-or-later port of the hashing engine of RAHasher 1.8.3
(`rcheevos` commit `40d916d`, MIT) that links the GPL-2.0-or-later RVZSharp
library. This file lists every third-party component that contributes code,
behavior, data, or test vectors to this project, with its license and
provenance.

## Components

| Component | Version/Pin | License | Provenance / Use |
|---|---|---|---|
| rcheevos (rc_hash engine) | commit `40d916de00fe757bab40fb4db41a7912193a48e3` | MIT — Copyright (c) 2018 RetroAchievements.org | Ported 1:1 into `RetroAchievementsSharp` (see the MIT notice below); test vectors under `test/rhash/` ported into `RetroAchievementsSharp.Tests` |
| CHDSharp | 1.2.0 (NuGet) | MIT — Copyright (c) 2026 Peterson Fernandes and Gordon Jefferyes | CHD V1–V5 reading in `ChdCdReader` |
| RVZSharp | 1.0.0 (NuGet) | GPL-2.0-or-later (Dolphin-derived) | GameCube/Wii RVZ/WIA decoding in `RvzFilereader` (no rvz→iso conversion) |
| VideoGameFileSystemParser | 1.2.0 (NuGet) | MIT — Copyright (c) 2025 Peterson Fernandes | Alternative ISO9660/UDF filesystem backend (`FileSystemResolver`) |
| Serilog | 4.4.0 (NuGet) | Apache-2.0 | Logging in the library, CLI, and tests |
| Serilog.Sinks.Console | 6.1.1 (NuGet, CLI only) | Apache-2.0 | Console sink for the CLI (byte-exact parity output) |
| Meziantou.Analyzer | 3.0.157 (NuGet, build-time only) | MIT | Code-quality analyzers (never shipped) |
| Microsoft.SourceLink.GitHub | 10.0.400 (NuGet, PrivateAssets) | MIT — Copyright (c) .NET Foundation | SourceLink/SourceRevisionId in symbols and packages |

## The MIT notice of the ported rcheevos code

The hashing engine in `RetroAchievementsSharp` (and its test vectors) is ported from
[rcheevos](https://github.com/RetroAchievements/rcheevos), which is licensed
under the MIT License:

```
MIT License

Copyright (c) 2018 RetroAchievements.org

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## RVZSharp and its own dependencies (GPL-2.0-or-later, via RVZSharp)

RVZSharp is copyright (c) 2025-2026 Peterson Fernandes and Pure Logic Code,
licensed under GPL-2.0-or-later; its RVZ/WIA format logic is derived from
[Dolphin](https://github.com/dolphin-emu/dolphin)'s DiscIO module (GPL-2.0-or-later).
Because RetroAchievementsSharp links RVZSharp, the combined library is distributed under
GPL-2.0-or-later (see `LICENSE`).

RVZSharp's own runtime dependencies (all MIT):

- **SharpCompress** (MIT, © Adam Hathcock) — LZMA/LZMA2 decoder core,
  itself originating from the public-domain LZMA SDK by Igor Pavlov.
- **LZMA-SDK 22.1.1** (MIT, © Igor Pavlov) — LZMA1/LZMA2 encoder.
- **SharpZipLib 1.4.2** (MIT) — BZIP2 encoder/decoder.
- **ZstdSharp.Port 0.8.8** (MIT) — Zstandard encoder/decoder.

## GPL reference material (NOT shipped)

The following are used **only as read-only behavioral references** while
writing the C# implementation, and are **not** included in the RetroAchievementsSharp
sources, binaries, or NuGet package:

- `RAHasher-1.8.3` — RAHasher CLI sources (`RAHasher.cpp`, `Util.cpp`,
  `Hash3DS.cpp`, `HashCHD.cpp`, `Logger.*`), GPL-3.0 (RALibretro lineage,
  [LeXofLeviafan](https://github.com/LeXofLeviafan/) fork). RetroAchievementsSharp's `Program.cs`, `FileUtil.cs`, `Hash3DS.cs`,
  and `ChdCdReader.cs` are new implementations written to match observable
  behavior only; no GPL text is copied. **The parity test suite uses
  LeXofLeviafan's RAHasher binaries as the reference oracle** — his project
  is what we test our solution against.
- `RAHasher.exe` (test oracle for the parity harness) — built from the
  GPL-3.0 sources above, authored by
  [LeXofLeviafan](https://github.com/LeXofLeviafan/) (see
  [RALibretro](https://github.com/libretro/RAHasher) and his fork); lives in
  `References\` / `tools\` only, never shipped.
- `rcheevos-12.4.0` / `rcheevos-40d916d` — rcheevos sources used as the
  reference for the port and the oracle; MIT.
- `api_probe` / `oracle_probes` — small probing tools and scripts used to
  characterize oracle behavior during the port; not shipped.