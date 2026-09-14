# SimpleLauncher.ResourceTranslator

Console application that uses the **OpenRouter API** to automatically translate missing English resource keys into all other language files for both UI projects of [SimpleLauncher](../README.md):

- **WPF app** (`SimpleLauncher`): `SimpleLauncher\resources\strings.*.xaml` — master file `strings.en.xaml`
- **Avalonia app** (`SimpleLauncher.Avalonia`): `SimpleLauncher.Avalonia\Resources\strings.*.json` — master file `strings.en.json`

## What It Does

1. Loads the English master file of each project as the canonical key list.
2. Compares every other language file against English.
3. **Auto-removes duplicate keys** found in target language files.
4. **Translates missing keys** in batches of 40 via the OpenRouter chat-completions API.
5. **Preserves empty values** from English as empty entries in target languages.
6. **Re-sorts** each resource file alphabetically by key after writing.
7. Writes files as **readable UTF-8** — non-ASCII characters (Arabic, CJK, etc.) are written as-is, never as `\uXXXX` escape sequences.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [OpenRouter API key](https://openrouter.ai/keys) (the app **prompts every run** and does **not** store it)

## Running

```bash
dotnet run --project SimpleLauncher.ResourceTranslator
```

### Workflow

1. The app locates both resource folders automatically from the solution structure.
2. Enter your OpenRouter API key when prompted.
3. Select a model (or press Enter for the default `z-ai/glm-5.3-flash`).
4. It prints an analysis summary per project: how many languages need updates, how many keys are missing, and how many duplicates will be removed.
5. Press any key to proceed.
6. Keys are translated in batches of 40 with a 500 ms delay between requests to avoid rate limits.
7. Each language file is updated and saved automatically.

### When to run it

The `SimpleLauncher.Avalonia.Tests` resource tests tell you:

- `DetectMissingResourceStringsTests` scans the Avalonia source (`.cs` `GetString(...)` calls and `.axaml` `{ext:Translate Key}` usages) and **auto-adds any missing key to `strings.en.json`** with a sensible fallback value.
- `LocalizationTests.EveryLanguageFileSharesTheEnglishKeySet` fails with a per-language list of missing keys and a pointer to this tool whenever a language file falls out of sync.

If either test fails, run the translator to propagate the new keys to all languages, then re-run the tests.

## Models

Default: `z-ai/glm-5.3-flash` (marked `(default)` in the prompt). Also selectable:

| Model | Notes |
| --- | --- |
| `z-ai/glm-5.3-flash` | default; cheap and fast |
| `deepseek/deepseek-v4-flash` | cheapest DeepSeek flash variant |
| `qwen/qwen3.7-flash` | cheapest Qwen flash variant |
| `qwen/qwen3.8-flash` | newer Qwen flash variant |
| `deepseek/deepseek-v4-pro-0813` | higher quality, slower and pricier |

Notes on model behavior:

- The request sends **no `reasoning` parameter**. Some endpoints (e.g. `z-ai/glm-5.3-flash`) mandate reasoning and reject `reasoning: { enabled: false }` with HTTP 400; thinking output arrives in a separate `message.reasoning` field that is ignored.
- `HttpClient` timeout is **10 minutes** per batch, so thinking models do not time out.

## Project Structure

```
SimpleLauncher.ResourceTranslator/
├── SimpleLauncher.ResourceTranslator.csproj
├── Program.cs                          # Entry point, user prompts, orchestration (WPF + Avalonia)
├── Models/
│   ├── OpenRouterModelInfo.cs          # Model metadata (id, name, description)
│   └── MissingKeyBatch.cs              # Holds missing keys & duplicates per language
└── Services/
    ├── ResourceAnalyzer.cs             # Reads English XAML keys and diffs other XAML languages
    ├── JsonResourceAnalyzer.cs         # Reads English JSON keys and diffs other JSON languages
    ├── OpenRouterTranslationService.cs # HTTP client for OpenRouter API batch translation
    ├── XamlResourceWriter.cs           # Writes updated XAML, removes duplicates, sorts keys
    └── JsonResourceWriter.cs           # Writes updated JSON (UTF-8 without BOM, 2-space indent, readable)
```

## Configuration

No configuration files are used. The only runtime inputs are:

- **API key** (typed interactively, never persisted)
- **Model selection** (default: `z-ai/glm-5.3-flash`)

## Translation protocol

The LLM receives each batch as a flat list of `Key|Value` lines and must answer with one `Key|Translated value` line per entry. To keep the protocol unambiguous:

- Newlines inside values are escaped as literal `\n` (and back-translated on parse); literal pipes in values are escaped as `\|`.
- Non-`Key|Value` lines in the response (reasoning echoes, markdown, explanations) are ignored by the parser.

## Notes

- If a translation batch fails (network error, rate limit, etc.), the app **skips that batch** and does not add empty strings to the resource file. Run the app again later to retry.
- The app is safe to run multiple times; it only processes keys that are actually missing.
- Empty English values are intentionally preserved as empty entries so translators can fill them in later.
- JSON files are written as UTF-8 without a BOM, with 2-space indentation and `StringComparer.OrdinalIgnoreCase` key order — matching the committed Avalonia resource files.
