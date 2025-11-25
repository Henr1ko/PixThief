# 🖼️ PixThief

**The image scraper that actually works on modern websites.**

No config files. No CLI flags to memorize. No Python environment to set up. Just run it, paste a URL, and grab your images.

[![Release](https://img.shields.io/badge/release-v2.0.0-blue)](https://github.com/Henr1ko/PixThief/releases/tag/v2.0.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)]()

---

## ⚡ Quick Start

1. **[Download the latest release](https://github.com/Henr1ko/PixThief/releases/tag/v2.0.0)**
2. Run `PixThief.exe`
3. Enter a URL
4. Hit Start

That's it. No really, that's it.

---

## ✨ Features

### 🧠 Actually Works on Modern Sites
Most scrapers choke on JavaScript-heavy sites. PixThief has **built-in browser rendering** (Playwright) that handles React, Vue, lazy-loading, infinite scroll - all the stuff that breaks other tools. Toggle it on, and suddenly that "impossible" site just works.

### 🎨 Beautiful TUI
No more squinting at `--help` output. PixThief has a clean, interactive terminal interface. Navigate with arrow keys, see your settings at a glance, watch downloads in real-time. It's actually pleasant to use.

### 🚀 Fast by Default
Parallel downloads (up to 32 threads), smart deduplication so you don't download the same image twice, and stealth mode to avoid getting blocked. Out of the box. No tuning required.

### 🔄 Resume Downloads
Internet died? Laptop crashed? PixThief saves checkpoints automatically. Just run it again and pick up where you left off.

### 🎛️ Powerful When You Need It
Simple by default, but the options are there when you want them:

- Filter by image size (skip thumbnails, target specific dimensions)
- Crawl entire domains or single pages
- Convert formats on the fly (PNG → JPG, etc.)
- Organize output (by date, by page, mirrored structure)
- Respect robots.txt (because we're not animals)

---

## 🛠️ System Requirements

- Windows x64
- ~200MB disk space
- That's it (runtime is included)

---

## 🎮 Usage Examples

### "I just want the images from this page"
1. Run PixThief
2. Set URL → paste your link
3. Single Page mode
4. Start Download
5. Done

### "I want EVERYTHING from this website"
1. Run PixThief
2. Set URL → paste the homepage
3. Entire Domain mode
4. Maybe bump up concurrency to 8-16
5. Enable Stealth Mode (be nice to servers)
6. Start Download
7. Go make coffee

### "This site is full of JavaScript garbage"
1. Run PixThief
2. Set your URL
3. Advanced Settings → Enable JS Rendering
4. Start Download
5. Watch it work anyway 😎

---

## 🔥 Pro Tips

- **Stealth Mode** adds randomized delays between requests. Slower, but way less likely to get blocked.
- **Skip Thumbnails** filters out images smaller than 200x200px. Saves you from downloading 10,000 tiny icons.
- **JS Rendering** is the secret weapon. If a site looks broken or empty, turn this on.
- **Checkpoints** are automatic in domain mode. If something goes wrong, just restart - it remembers.

---

## 📥 Download

**[→ Get PixThief v2.0.0](https://github.com/Henr1ko/PixThief/releases/tag/v2.0.0)**

Just download, extract, and run. No installation needed.

---

## 🤝 Contributing

Found a bug? Got an idea? Open an issue or PR. Keep it chill.

---

## 📜 License

MIT - Do whatever you want with it.

---

<p align="center">
  <i>Built because every other scraper was annoying to use.</i>
</p>
