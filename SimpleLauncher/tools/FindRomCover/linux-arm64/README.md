[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/releases)
[![CI](https://github.com/purelogiccode/FindRomCover/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/FindRomCover/actions/workflows/ci.yml)
[![Docs](https://github.com/purelogiccode/FindRomCover/actions/workflows/docs.yml/badge.svg)](https://github.com/purelogiccode/FindRomCover/actions/workflows/docs.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)](https://github.com/purelogiccode/FindRomCover/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/FindRomCover/total?label=downloads)](https://github.com/purelogiccode/FindRomCover/releases)
[![GitHub stars](https://img.shields.io/github/stars/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/stargazers)
[![Issues](https://img.shields.io/github/issues/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/issues)
[![Last commit](https://img.shields.io/github/last-commit/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/commits/master)
[![Docs site](https://img.shields.io/badge/docs-purelogiccode.github.io-blue)](https://purelogiccode.github.io/FindRomCover/)
[![Wiki](https://img.shields.io/badge/wiki-available-blue)](https://github.com/purelogiccode/FindRomCover/wiki)
[![Contributors](https://img.shields.io/github/contributors/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/graphs/contributors)
[![Forks](https://img.shields.io/github/forks/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/forks)
[![Pull requests](https://img.shields.io/github/issues-pr/purelogiccode/FindRomCover?label=pull%20requests)](https://github.com/purelogiccode/FindRomCover/pulls)
[![Open bugs](https://img.shields.io/github/issues/purelogiccode/FindRomCover/bug?label=open%20bugs)](https://github.com/purelogiccode/FindRomCover/issues)
[![Commit activity](https://img.shields.io/github/commit-activity/m/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/commits/master)
[![Release date](https://img.shields.io/github/release-date/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover/releases)
[![Latest downloads](https://img.shields.io/github/downloads/purelogiccode/FindRomCover/latest/total?label=latest%20downloads)](https://github.com/purelogiccode/FindRomCover/releases/latest)
[![Repo size](https://img.shields.io/github/repo-size/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover)
[![Code size](https://img.shields.io/github/languages/code-size/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover)
[![Top language](https://img.shields.io/github/languages/top/purelogiccode/FindRomCover)](https://github.com/purelogiccode/FindRomCover)
[![C# 14](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![UI: Avalonia 12](https://img.shields.io/badge/UI-Avalonia%2012-8B44AC)](https://avaloniaui.net)
[![Tests](https://img.shields.io/badge/tests-879%20passing-brightgreen)](https://github.com/purelogiccode/FindRomCover/actions/workflows/ci.yml)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](https://github.com/purelogiccode/FindRomCover/blob/master/docs/development/contributing.md)
[![Maintained](https://img.shields.io/badge/maintained-yes-brightgreen)](https://github.com/purelogiccode/FindRomCover/commits/master)
[![Discussions](https://img.shields.io/badge/discussions-available-blue?logo=github)](https://github.com/purelogiccode/FindRomCover/discussions)
[![Donate](https://img.shields.io/badge/donate-Pure%20Logic%20Code-ff69b4)](https://www.purelogiccode.com/donate)

# FindRomCover

A powerful cross-platform desktop application (Windows, Linux, and macOS) designed to help you automatically find and download missing cover art for your retro gaming ROM collection.
It supports **Local Similarity Search** across your existing image folder, plus **Google Web Image Search**, **Bing Web Image Search**, and the **Google Custom Search API** to fetch high-quality game cover images. Version **3.3** migrated the app to Avalonia and added an optional **AI Vision Assist** that looks at the candidates and picks the best cover for you.

![Main Window](screenshot.png)

![Main Window](screenshot2.png)

## Documentation

- **Documentation site**: [purelogiccode.github.io/FindRomCover](https://purelogiccode.github.io/FindRomCover/)
- **Wiki**: [github.com/purelogiccode/FindRomCover/wiki](https://github.com/purelogiccode/FindRomCover/wiki)
- **What's New in 3.3**: [WhatsNew.md](WhatsNew.md)
- **Source**: [`docs/`](docs/) — built with MkDocs Material and synced to the wiki by CI
- **Recommended AI models**: [RecommendedModels.md](RecommendedModels.md)

## Features

- **Smart Search**: Automatically searches for game covers using cleaned ROM filenames.
- **MAME Integration**: Leverages MAME database for accurate game titles and descriptions.
- **Batch Processing**: Scan entire ROM directories to identify missing covers.
- **Multiple Sources**:
    - **Local Similarity Search**: Matches ROM filenames against images already in your local image folder using configurable similarity algorithms (Jaccard Similarity, Jaro-Winkler Distance, or Levenshtein Distance) with an adjustable match threshold (10-90%).
    - **Ignore Bracketed Text in Matching**: On by default; ignores text inside `()`, `[]`, and `{}` when comparing names, so `Game (USA) [En]` matches a cover named `Game`. Toggle it from the Settings menu.
    - **Google Web Image Search**: Uses the embedded native browser (WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux) to display Google image search results.
    - **Bing Web Image Search**: Uses the embedded native browser (WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux) to display Bing image search results.
    - **Google Custom Search API**: Fetches image results directly via API (requires an API key).
- **Real-time Preview**: Thumbnail previews with configurable sizes (100-500px).
- **Customizable UI**: Light/Dark themes with 20+ accent colors.
- **Missing Covers List**: Automatically generates a list of ROMs without corresponding cover art (checks for PNG, JPG/JPEG, BMP, GIF, TIFF/TIF, WebP, AVIF, HEIC/HEIF, ICO, SVG, JXL, JP2). Right-click an entry to remove it from the list, copy its filename, or delete the corresponding ROM/ISO file.
- **Flexible Configuration**: Support for custom file extensions and search queries.
- **Automatic Image Conversion**: Automatically converts downloaded images (JPG, BMP, GIF, TIFF, WebP, AVIF, HEIC, HEIF, JXL, JP2, ICO, SVG) to PNG format using Magick.NET (ImageMagick). Also, automatically converts newly saved images in the image folder to PNG.
- **Automatic File Rename & Match**: A built-in `FileSystemWatcher` monitors the image folder and automatically renames any newly saved image to match the currently selected missing ROM's filename, then converts it to PNG and removes the entry from the missing covers list — so images saved from the embedded browser are matched hands-free.
- **AI Vision Assist**: Connect a vision-capable model (OpenRouter, OpenAI, Anthropic, Gemini or GLM cloud, a local Ollama/LM Studio server, or a custom OpenAI/Anthropic-compatible endpoint) to actually *look* at candidate images and pick the correct cover. Works on Local Files and Google API results, can auto-save above a confidence threshold, verify images before renaming or saving, and batch-fill the entire missing list.
- **AI Batch Fill**: Processes the whole missing-covers list — local candidates first, optional Google API fallback — with live progress, cancel, per-item results, and a query history that avoids asking the AI twice.
- **Safe Cover Saves**: The folder watcher never overwrites an existing cover, and Google API downloads are verified before a file is replaced.
- **Detailed Logging**: Built-in log viewer for troubleshooting and rolling `app<yyyyMMdd>.log` files in the per-user data folder (`%LocalAppData%\FindRomCover` on Windows, `~/.local/share/FindRomCover` on Linux, `~/Library/Application Support/FindRomCover` on macOS) using Serilog.
- **Sound Feedback**: Optional audio feedback for user actions (MCI on Windows, `afplay` on macOS, common command-line players on Linux).
- **Command-line Arguments**: Start the application with pre-set ROM and Image folders for quick scanning.
- **Auto-Copy to Clipboard**: Automatically copies the selected ROM filename to the clipboard when navigating the missing covers list.
- **Automatic Updates**: Checks for new releases on startup via GitHub and notifies you when an update is available, with release notes and direct download links.
- **System Tray Integration**: Minimize to system tray with balloon notifications and quick restore functionality.
- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection for clean architecture and testability.
- **Comprehensive Testing**: Full test suite with xUnit, FluentAssertions, and Moq.

## Supported File Types

You can add or remove supported extensions through the `Settings > Edit Supported Extensions...` menu.

**Recognized Cover Image Formats**: The application recognizes and converts `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.tiff`, `.tif`, `.webp`, `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2`, `.ico`, and `.svg` files to `.png` format.

## Getting Started

### Prerequisites

- Windows 10 or later, a mainstream Linux distribution, or macOS 11 or later (x64 and ARM64 on Windows and Linux; Apple Silicon on macOS)
- **.NET 10.0 Runtime**: The released builds are framework-dependent. If it is not already installed, download it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) (choose ".NET Desktop Runtime" on Windows).
- **Native web view runtime**: Essential for Bing and Google Web Image Search — Microsoft Edge WebView2 on Windows (pre-installed on most systems), WebKitGTK on Linux, WKWebView on macOS (part of the OS). If missing, the application prompts you with a download link and falls back to the system browser.
- (Optional) Valid API key for Google Custom Search API if you choose to use that search method.

### Installation

1. **Download the latest release** from the [Releases](https://github.com/purelogiccode/FindRomCover/releases) page:

    | File | Platform |
    |------|----------|
    | `release_<version>_win-x64.zip` | Windows x64 |
    | `release_<version>_win-arm64.zip` | Windows ARM64 |
    | `release_<version>_linux-x64.zip` | Linux x64 |
    | `release_<version>_linux-arm64.zip` | Linux ARM64 |
    | `release_<version>_osx-arm64.zip` | macOS Apple Silicon |

    Each archive also contains `LICENSE.txt`, `README.md`, and `WhatsNew.md`. There is no Intel-Mac build because Magick.NET 15 no longer ships a macOS x64 native library.
2. **Extract** the archive to a folder of your choice.
3. **Run** `FindRomCover.exe` on Windows, or the `FindRomCover` executable on Linux/macOS (you may need `chmod +x FindRomCover` first).
4. **Configure API keys** (if using Google API search, see Setup section below).

### API Setup (Only for Google Custom Search API)

The "Local Files", "Google Web", and "Bing Web" options do **not** require any API keys. Local Files searches your own image folder, and the web options use an embedded browser to display results.

To use the **Google Custom Search API**:
1. **Open the application.**
2. Navigate to `Settings > API Settings` in the menu.
3. **Google Custom Search API**:
    - Go to [Google Cloud Console](https://console.cloud.google.com).
    - Enable the "Custom Search JSON API".
    - Create an API key.
    - Enter your Google API key into the `API Settings` window.
    - Click "Save".

### AI Setup (Optional, for AI Vision Assist)

AI Vision Assist uses a vision-capable model to choose the best cover among candidates.

1. Open `Settings > AI Settings...`.
2. Choose a provider:
    - **OpenRouter** (cloud): paste an API key from [openrouter.ai/keys](https://openrouter.ai/keys). Default model: `google/gemma-4-26b-a4b-it`.
    - **OpenAI** (cloud): paste an API key from [platform.openai.com/api-keys](https://platform.openai.com/api-keys). Default model: `gpt-4o-mini`.
    - **Anthropic** (cloud): paste an API key from [console.anthropic.com](https://console.anthropic.com). Default model: `claude-sonnet-4-5`.
    - **Gemini** (cloud): paste an API key from [aistudio.google.com](https://aistudio.google.com/apikey). Default model: `gemini-2.5-flash`.
    - **GLM** (cloud): paste a Zhipu/Z.AI API key from [z.ai](https://z.ai). Default model: `glm-4.5v`. For the China endpoint, change the Base URL to `https://open.bigmodel.cn/api/paas/v4`.
    - **Local**: run [Ollama](https://ollama.com) (default `http://localhost:11434/v1`, model e.g. `qwen2.5vl:7b`) or LM Studio (`http://localhost:1234/v1`). No API key needed.
    - **Custom (OpenAI-compatible)** / **Custom (Anthropic-compatible)**: point at any compatible endpoint (proxy, gateway, self-hosted server). Enter the Base URL and Model; the API key is optional.
3. Click **Test / Load Models** to verify the connection and load the model list. The picker defaults to **Vision-capable only** (models that most likely accept image input), can be searched with the filter box, and the loaded list is cached per provider for 7 days.
4. Enable **AI-assisted image selection** and optionally:
    - **Run AI Pick automatically** after each search.
    - **Automatically save the AI pick** when confidence is above the threshold.
    - **Verify images with AI** before renaming or saving.
    - **Candidate similarity (%)**: only local images whose filename similarity (using the algorithm selected in the main window) is at or above this value are sent to the AI. Raise it to reduce cost and noise; lower it to let the AI look at weaker filename matches.
    - **Skip missing covers already queried in previous sessions**: queried items are remembered so the AI is not asked twice for the same cover; use **Clear query history** to start over.
5. Use the **AI Pick Best** button on the Local Files and Google API tabs, or **AI Fill Missing Covers...** on the missing covers list for batch processing.

Images are downscaled before upload (default 512px) and verdicts are cached for 30 days to limit API usage. Queried covers are remembered in the SQLite database `QueryHistory.dat` inside the per-user data folder (180 days).

### Settings Storage

All settings — theme, folders, similarity options, Google key, AI provider, model and API keys — are stored in the SQLite database `Settings.dat` inside the per-user data folder (`%LocalAppData%\FindRomCover` on Windows, `~/.local/share/FindRomCover` on Linux, `~/Library/Application Support/FindRomCover` on macOS). API keys are encrypted before being written. On first start after upgrading from an older build, the previous encrypted `settings.dat` file is migrated automatically and kept as `settings.dat.legacy` as a backup.

## Usage Guide

### Basic Workflow

1. **Setup Directories**
   - ROM Folder: Where your game files are stored.
   - Image Folder: Where you want cover images saved.
   - Click "Browse..." to select folders. As soon as both folders are set, the app scans automatically and lists the results — you can also press "Check for Missing Images" (or F5) to re-scan at any time.
   - The status bar reports how many ROM files matched the supported extensions and how many are missing covers.

2. **Missing Covers List**
   - The app lists all ROMs without a corresponding `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.tiff`, `.tif`, `.webp`, `.avif`, `.heic`, `.heif`, `.jxl`, `.jp2`, `.ico`, or `.svg` cover in your image folder.
   - If no ROM files are found, the status bar tells you explicitly so you can verify the folder and supported extensions.

3. **Find Covers**
   - Select a game from the missing covers list.
   - The app automatically searches for cover images using your selected search engine.
   - On the **Local Files** tab, it lists images from your image folder whose names are similar to the ROM, ranked by the selected similarity algorithm and threshold.
   - If using "Web Search" (Google/Bing Web), the results will appear in the embedded browser.
   - If using "Google API", image suggestions will appear as clickable thumbnails.

4. **Download Covers**
   - **For Local Files / API Search**: Click on the cover image you want from the suggestions. The image is automatically converted to PNG (if necessary) and saved as `[gamename].png`.
   - **For Web Search**: Right-click on an image in the embedded browser and choose "Save image as...". Save it inside the Image Folder using any filename. The application's `FileSystemWatcher` will then detect the new image, automatically rename it to match the currently selected missing ROM's filename, convert it to PNG if needed, and remove the game from the missing list. *Note: Automatic saving directly from the web view is not available due to browser security restrictions.*
   - The game is removed from the missing covers list once a corresponding PNG is detected in the image folder.

### Advanced Features

#### Custom Search Queries
Add extra search terms in the "Extra Query" field:
- `"box art"` - for box art specifically
- `"front cover"` - for front covers only
- `"game cover"` - for cover images

#### Theme Customization
Access through the menu:
- **Theme > Base Theme** - Switch between Light and Dark.
- **Theme > Accent Colors** - Choose from 20+ color schemes.

#### Search Engine Selection
Switch between "Local Files", "Google Web", "Bing Web", and "Google API" using the tabs above the results area.

#### Similarity Algorithm & Threshold (Local Files)
Configure how local image names are matched to your ROMs:
- **Set Similarity Algorithm** - Choose between Jaccard Similarity, Jaro-Winkler Distance, and Levenshtein Distance.
- **Set Similarity Threshold** - Set the minimum match percentage (10-90%) required to list a local image.
- **Ignore Bracketed Text in Matching** - Enabled by default; balanced `(...)`, `[...]`, and `{...}` groups are ignored on both sides of the comparison, so `Game (USA) [En]` matches a cover named `Game`. Turn it off to compare the full filenames.

#### AI Batch Fill
Click **AI Fill Missing Covers...** above the missing covers list to process many games in one run. The window lets you:
- control the number of items per run (default 25, max 500);
- enable or disable the Google API fallback (enabled when a Google API key is configured);
- skip covers that were already queried in previous sessions;
- watch live per-item results and cancel after the current item.

Covers are saved only when the model's confidence is at or above the **Auto-save threshold** (AI Settings), and each saved cover is removed from the missing list automatically.

#### Thumbnail Size
Adjust preview sizes for API search results:
- **Set Thumbnail Size** menu (100-500px).

#### MAME Descriptions
Toggle the use of MAME descriptions for search queries:
- **Settings > Use MAME Descriptions** - When enabled, the app will use the full MAME game description instead of the cleaned ROM filename for searches.

#### Log Window
Access detailed logs for troubleshooting:
- **Settings > Show/Hide Log Window**.
- Log files are also saved as rolling `app<yyyyMMdd>.log` files in the per-user data folder.
- `UserLogError.txt` contains a simplified list of errors for user reference, and `ApiLogError.txt` holds reports queued for the automatic bug-report API.

#### Command-line Arguments
You can launch the app with command-line arguments to pre-fill the ROM and Image folders (`FindRomCover.exe "C:\Path\To\ImageFolder" "C:\Path\To\RomFolder"` on Windows, `./FindRomCover "/path/to/image" "/path/to/roms"` on Linux/macOS):
- `FindRomCover[.exe] "<ImageFolder>" "<RomFolder>"`
- If only one argument is provided, it will be treated as the Image Folder.
- If both are provided, the application will automatically trigger a scan for missing images on startup.

#### System Tray
The application can be minimized to the system tray for background operation:
- Minimize the window to send it to the system tray.
- Right-click the tray icon to restore or exit the application.
- Balloon notifications inform you when the app is minimized.

## Architecture

The application follows a clean architecture pattern with separation of concerns:

### Project Structure
```
FindRomCover/
├── ApiProvider/          # External API integrations (Google Custom Search)
├── Managers/             # Settings, settings database, MAME data
├── models/               # Data models and DTOs
├── Services/             # Business logic and utilities
│   └── Ai/               # AI vision subsystem (clients, picking, batch fill)
├── audio/                # UI sound assets
├── icon/                 # Application icon
└── images/               # UI image assets
```

Windows (Avalonia `Window`s: main window, settings, AI, batch, log, about) live in the project root next to `App.axaml.cs` and the `MainWindow.*.cs` partial files. Interfaces live in `Interfaces/`, models and enums in `Models/`. The legacy WPF project is kept in `FindRomCover_Legacy/` and is no longer built.

### Key Components

- **App.axaml.cs**: Application entry point with dependency injection setup, global exception handling, and theme management.
- **MainWindow**: Main application window with tabbed interface for different search methods.
- **Services**: Modular services for specific functionality:
  - `ImageProcessor`: Image conversion and processing using Magick.NET
  - `ImageFolderWatcher`: Real-time file system monitoring for automatic image detection
  - `AiAssistService`: Vision-model ranking/verification of cover candidates (OpenRouter, OpenAI, Anthropic, Gemini, GLM or local/custom OpenAI- and Anthropic-compatible servers)
  - `AiBatchFillService`: Batch processing of the missing covers list with optional Google API fallback and query history
  - `AiQueryHistory`: SQLite-backed memory of already-queried covers (180 days)
  - `AiVerdictCache`: 30-day cache of AI picks and verification results
  - `SimilarityCalculator`: Local image name matching via Jaccard, Jaro-Winkler, and Levenshtein algorithms
  - `WebSearchService`: URL generation for web searches
  - `SearchQueryHelper`: ROM filename cleaning and sanitization
  - `UpdateCheckService`: GitHub release checking
   - `SystemTrayIcon`: System tray integration (via `TrayIconManager`)
  - `LogService`: Centralized logging with Serilog

### Dependencies

- **Avalonia**: Cross-platform .NET UI framework (Windows, Linux, macOS)
- **Avalonia.Controls.WebView (NativeWebView)**: Embedded web browser for search results
- **Magick.NET**: Image processing and format conversion
- **Serilog**: Structured logging
- **Microsoft.Extensions.DependencyInjection**: Dependency injection container
- **MessagePack**: Efficient binary serialization for the MAME database
- **Microsoft.Data.Sqlite**: SQLite storage for settings and AI query history

## Troubleshooting

### Common Issues

**"API Key is not set" error (for Google API search)**
- If prompted, ensure your Google API key is correctly entered in `Settings > API Settings`.
- Verify the key is active and has sufficient quota in your Google Cloud Console.

**"Web view runtime missing" or "Web view component is not ready" error**
- The Bing/Google web tabs need the platform web view: Microsoft Edge WebView2 on Windows, WebKitGTK on Linux, WKWebView on macOS (part of the OS).
- On first launch, or if the component is missing, the application will prompt you with the download page for your platform. Until the runtime is installed, web searches open in your system browser instead.
- After installation, you may need to restart FindRomCover.

**No search results**
- Check your internet connection.
- Try different search terms in "Extra Query".
- Switch between "Local Files", "Google Web", "Bing Web", and "Google API" search engines.
- For local searches, lower the similarity threshold or try a different similarity algorithm.
- If using Google API, check your API key and Search Engine ID.

**Images not saving or converting automatically**
- Ensure the image folder has write permissions.
- For web searches, remember that you need to manually save images from the embedded browser. The application's `FileSystemWatcher` will then detect the new file, rename it to match the selected ROM, convert it to PNG if needed, and update the missing list.
- The watcher keeps an existing cover untouched rather than overwriting it; when saving from the Google API tab, the app asks before replacing an existing cover.
- Check the rolling log file in `%LocalAppData%\FindRomCover` for any errors related to file access or image conversion.

**Missing or Corrupted MAME Data File (`mame.dat`)**
- If you encounter errors about a missing `mame.dat` file, ensure it is present in the same directory as the application executable.
- If the file is present but errors about corruption occur, try obtaining a fresh copy of `mame.dat`.
- MAME descriptions will not be available if this file is missing or corrupted.

### Logs
Access detailed logs via:
- **Settings > Show/Hide Log Window**.
- Log files are saved as rolling `app<yyyyMMdd>.log` files in the per-user data folder.
- `UserLogError.txt` contains a simplified list of errors for user reference, and `ApiLogError.txt` holds reports queued for the automatic bug-report API.

## Testing

The project includes a comprehensive test suite using:

- **xUnit**: Test framework
- **FluentAssertions**: Fluent assertion library
- **Moq**: Mocking framework
- **coverlet**: Code coverage

Run tests with:
```bash
dotnet test
```

## Building from Source

### Prerequisites
- .NET 10.0 SDK
- Visual Studio 2022 or later (recommended)

### Build Steps
```bash
git clone https://github.com/purelogiccode/FindRomCover.git
cd FindRomCover
dotnet build
```

### Publishing
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
dotnet publish -c Release -r win-arm64 --self-contained false -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=true
dotnet publish -c Release -r linux-arm64 --self-contained false -p:PublishSingleFile=true
dotnet publish -c Release -r osx-x64 --self-contained false -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained false -p:PublishSingleFile=true
```

All release builds are framework-dependent single files (the .NET runtime is not embedded). Official releases are produced by CI — see [CI/CD](docs/development/ci-cd.md).

## Acknowledgments

- **Avalonia** for the cross-platform .NET UI framework.
- **Magick.NET** (ImageMagick) for robust image loading and conversion.
- **MessagePack** for efficient binary serialization.
- **MAME** team for the comprehensive arcade game database.
- **Avalonia WebView** for embedding web content.
- **Serilog** for structured logging.
- **xUnit**, **FluentAssertions**, and **Moq** for the testing framework.

## License

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE.txt](LICENSE.txt) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/purelogiccode/FindRomCover/issues)
- **Discussions**: [GitHub Discussions](https://github.com/purelogiccode/FindRomCover/discussions)
- **Donations**: [Support Development](https://www.purelogiccode.com/donate)

---

Made with ❤️ by [Pure Logic Code](https://www.purelogiccode.com)