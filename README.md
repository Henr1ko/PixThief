# PixThief – Advanced Image Web Scraper

**Version**: 2.0 – Complete TUI Rebuild  
**Build**: November 25, 2025  
**Status**: Production ready

PixThief is a .NET 8-powered image web scraper with a full-screen terminal UI, advanced crawling logic,
and lots of filtering options. Point it at a page (or a whole domain), tweak a few settings, and watch
the downloads roll in from a live dashboard.

---

## Features at a glance

- 🎛 **Full TUI dashboard** built with Spectre.Console  
  Live progress, download queue, stats, activity log – everything in the terminal.
- 🌐 **Single page or full domain crawl**  
  Limit by max pages and depth, optionally respect `robots.txt`.
- 🧠 **JavaScript rendering (Playwright)**  
  Optionally render pages with a real browser to catch dynamically loaded images.
- 🎯 **Aggressive filtering**  
  Filter by width/height, file size, filename regex, URL regex, GIF handling, and thumbnail skipping.
- 📁 **Flexible output layout**  
  Flat folder, per-page folders, date-based folders, or a mirrored site directory structure.
- 🔁 **Checkpoints & resume**  
  Save progress and resume long-running crawls later.
- 🧾 **Logging & configs**  
  Log to file, load settings from JSON, and reuse your favorite scraping profiles.

---

## Requirements

- .NET 8 SDK (for building/running from source)
- Windows x64 is the default target (`win-x64` runtime identifier)
- For JS rendering: Playwright browser binaries will be downloaded the first time JS mode is used

You can still tweak the project for other platforms if you want, but the default build is for Windows.

---

## Quick start

### 1. Using a prebuilt binary (PixThief.exe)

Just run it with no arguments to launch the TUI:

```bash
PixThief.exe
```

The TUI will guide you through:

- URL to scrape
- Single-page vs whole-domain mode
- Output folder and organization
- Image filters and download behavior
- Optional JS rendering and stealth options

Then it shows a live dashboard with progress, stats, and logs.

---

### 2. CLI mode (no TUI)

You can run PixThief fully from the command line if you prefer scripting.

**Basic usage:**

```bash
PixThief.exe <url> [options]
```

#### Core options

- `--domain`  
  Crawl the whole domain instead of just the starting page.
- `--config <file>`  
  Load settings from a JSON configuration file.

#### Output options

- `--out <folder>`  
  Set a custom output folder (default: auto-generated).
- `--organize <type>`  
  Decide how files are structured:
  - `flat`
  - `by-page`
  - `by-date`
  - `mirrored` (mirror site path structure)

#### Crawling options

- `--max-pages <n>` – limit how many pages are crawled (default: 100)  
- `--max-depth <n>` – link depth limit (0 for only the starting page, negative for unlimited)  
- `--robots-txt <true|false>` – whether to respect `robots.txt`

#### Download options

- `--concurrency <n>` – how many images to download in parallel (1–32, default: 4)  
- `--stealth` – randomize delays between requests for a more “human” pattern  
- `--delay <ms>` – fixed delay between requests (disables randomization)

#### Image filtering

- `--min-width <px>` / `--min-height <px>`  
- `--max-width <px>` / `--max-height <px>`  
- `--min-size <kb>` / `--max-size <kb>`  
- `--filename-pattern <regex>` – filter by filename using a regex  
- `--url-regex <regex>` – filter by image URL using a regex  
- `--include-gifs` – keep animated GIFs instead of skipping them  
- `--skip-thumbnails` – ignore small thumbnail-style images

#### Image conversion

- `--convert-to <fmt>` – convert downloaded images to `jpg`, `png`, or `gif`  
- `--jpeg-quality <n>` – quality for JPEG output (1–100, default around 90)

#### Advanced features

- `--enable-js` – enable JavaScript rendering via Playwright  
- `--js-wait <ms>` – how long to wait for JS (typical range: 500–30000 ms)  
- `--checkpoint <file>` – save checkpoints so you can resume later  
- `--log <file>` – log detailed activity to a specific file  
- `--verbose` – show extra info while running  
- `--help` – show the built-in help and examples

---

## Example commands

Scrape a single page with default behavior:

```bash
PixThief.exe https://example.com
```

Crawl an entire domain with JS rendering and stealth mode:

```bash
PixThief.exe https://example.com --domain --enable-js --stealth
```

Download only reasonably large images and avoid huge files:

```bash
PixThief.exe https://example.com --min-width 640 --min-height 480 --max-size 5000
```

Organize by date with more parallel downloads:

```bash
PixThief.exe https://example.com --domain --concurrency 8 --organize by-date
```

Use a configuration file:

```bash
PixThief.exe https://example.com --config settings.json
```

---

## Configuration files

PixThief can load settings from a JSON config file using `--config <file>`.

A config file typically stores things like:

- Default output folder
- Crawl limits (pages, depth)
- Concurrency and delays
- Stealth mode and JS rendering preferences
- Filter thresholds (size, dimensions)
- Logging and checkpoint preferences

You can create a config by starting from a crawl you like and saving out the important values,
then reusing that file with `--config` in future runs.

---

## Building from source

Clone and build with the .NET 8 SDK:

```bash
git clone https://github.com/Henr1ko/PixThief.git
cd PixThief/getpics
dotnet restore
dotnet run
```

For a self-contained Windows x64 build (includes the runtime):

```bash
dotnet publish -c Release -r win-x64
```

For a lighter build that depends on an installed .NET 8 runtime,
you can use the `Lightweight` configuration defined in the project.

---

## License

PixThief is released under the MIT License. See `LICENSE` for full details.
