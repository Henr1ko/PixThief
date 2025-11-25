using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PixThief;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // If no arguments provided, launch the full TUI application
            if (args.Length == 0)
            {
                var tui = new TuiApplication();
                await tui.RunAsync();
                return 0;
            }

            // Otherwise, use CLI mode with interactive setup
            var options = ParseArguments(args);
            if (options == null || string.IsNullOrEmpty(options.Url))
            {
                return 1;
            }

            // Load configuration file if specified
            if (!string.IsNullOrEmpty(options.ConfigFile) && File.Exists(options.ConfigFile))
            {
                ApplyConfigFile(options, options.ConfigFile);
            }

            // Show interactive setup menu if URL was provided
            var setup = new InteractiveSetup(options);
            setup.ShowSetupMenu();

            // Create stats tracker
            var stats = new ProgressStats();

            // Create and run the enhanced scraper
            var scraper = new ImageScraperEnhanced(options, stats);

            if (options.IsDomainMode)
            {
                await scraper.DownloadImagesForDomainAsync();
            }
            else
            {
                await scraper.DownloadImagesForSinglePageAsync();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
    }

    static ScraperOptions? ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return null;
        }

        if (IsHelpOption(args[0]))
        {
            PrintUsage();
            return null;
        }

        var url = args[0];

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Console.Error.WriteLine("[ERROR] Invalid URL. Must be a valid HTTP or HTTPS URL.");
            return null;
        }

        var options = new ScraperOptions { Url = url };

        // Parse optional arguments
        for (int i = 1; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                // Core functionality
                case "--domain":
                    options.IsDomainMode = true;
                    break;

                case "--config":
                    if (!TryReadValueOption(args, ref i, "--config", out var configFile))
                        return null;
                    options.ConfigFile = configFile;
                    break;

                // Output options
                case "--out":
                    if (!TryReadValueOption(args, ref i, "--out", out var folder))
                        return null;
                    options.OutputFolder = SanitizeFileName(folder);
                    break;

                case "--organize":
                    if (!TryReadValueOption(args, ref i, "--organize", out var orgType))
                        return null;
                    if (orgType != "flat" && orgType != "by-page" && orgType != "by-date" && orgType != "mirrored")
                    {
                        Console.Error.WriteLine("[ERROR] --organize must be: flat, by-page, by-date, or mirrored");
                        return null;
                    }
                    options.OutputOrganization = orgType;
                    break;

                // Crawling options
                case "--max-pages":
                    if (!TryReadIntOption(args, ref i, "--max-pages", out var maxPages))
                        return null;
                    if (maxPages <= 0)
                    {
                        Console.Error.WriteLine("[ERROR] --max-pages must be a positive integer.");
                        return null;
                    }
                    options.MaxPages = maxPages;
                    break;

                case "--max-depth":
                    if (!TryReadIntOption(args, ref i, "--max-depth", out var maxDepth))
                        return null;
                    options.MaxDepth = maxDepth;
                    break;

                case "--robots-txt":
                    var robotsVal = args[i + 1]?.ToLower();
                    if (robotsVal == "false" || robotsVal == "0" || robotsVal == "no")
                    {
                        options.RespectRobotsTxt = false;
                        i++;
                    }
                    else
                    {
                        options.RespectRobotsTxt = true;
                    }
                    break;

                // Download options
                case "--concurrency":
                    if (!TryReadIntOption(args, ref i, "--concurrency", out var concurrency))
                        return null;
                    if (concurrency < 1 || concurrency > 32)
                    {
                        Console.Error.WriteLine("[ERROR] --concurrency must be between 1 and 32.");
                        return null;
                    }
                    options.MaxConcurrentDownloads = concurrency;
                    break;

                case "--stealth":
                    options.StealthMode = true;
                    options.RandomizeDelays = true;
                    break;

                case "--delay":
                    if (!TryReadIntOption(args, ref i, "--delay", out var delay))
                        return null;
                    if (delay <= 0)
                    {
                        Console.Error.WriteLine("[ERROR] --delay must be a positive number of milliseconds.");
                        return null;
                    }
                    options.RequestDelayMs = delay;
                    options.StealthMode = true;
                    options.RandomizeDelays = false;
                    break;

                // Image filtering options
                case "--min-width":
                    if (!TryReadIntOption(args, ref i, "--min-width", out var minWidth))
                        return null;
                    options.MinImageWidth = minWidth;
                    break;

                case "--min-height":
                    if (!TryReadIntOption(args, ref i, "--min-height", out var minHeight))
                        return null;
                    options.MinImageHeight = minHeight;
                    break;

                case "--max-width":
                    if (!TryReadIntOption(args, ref i, "--max-width", out var maxWidth))
                        return null;
                    options.MaxImageWidth = maxWidth;
                    break;

                case "--max-height":
                    if (!TryReadIntOption(args, ref i, "--max-height", out var maxHeight))
                        return null;
                    options.MaxImageHeight = maxHeight;
                    break;

                case "--min-size":
                    if (!TryReadIntOption(args, ref i, "--min-size", out var minSizeKb))
                        return null;
                    options.MinFileSizeKb = minSizeKb;
                    break;

                case "--max-size":
                    if (!TryReadIntOption(args, ref i, "--max-size", out var maxSizeKb))
                        return null;
                    options.MaxFileSizeKb = maxSizeKb;
                    break;

                case "--filename-pattern":
                    if (!TryReadValueOption(args, ref i, "--filename-pattern", out var filenamePattern))
                        return null;
                    options.FilenamePattern = filenamePattern;
                    break;

                case "--url-regex":
                    if (!TryReadValueOption(args, ref i, "--url-regex", out var urlRegex))
                        return null;
                    try
                    {
                        new Regex(urlRegex);
                        options.UrlRegexFilter = urlRegex;
                    }
                    catch
                    {
                        Console.Error.WriteLine("[ERROR] Invalid regex pattern for --url-regex.");
                        return null;
                    }
                    break;

                // Image conversion
                case "--convert-to":
                    if (!TryReadValueOption(args, ref i, "--convert-to", out var formatRaw))
                        return null;
                    var format = formatRaw.ToLowerInvariant();
                    if (format == "jpeg")
                        format = "jpg";
                    if (format != "jpg" && format != "png" && format != "gif")
                    {
                        Console.Error.WriteLine("[ERROR] Invalid value for --convert-to. Use: jpg, png, or gif.");
                        return null;
                    }
                    options.ConvertToFormat = format;
                    break;

                case "--jpeg-quality":
                    if (!TryReadIntOption(args, ref i, "--jpeg-quality", out var quality))
                        return null;
                    if (quality < 1 || quality > 100)
                    {
                        Console.Error.WriteLine("[ERROR] --jpeg-quality must be between 1 and 100.");
                        return null;
                    }
                    options.JpegQuality = quality;
                    break;

                case "--include-gifs":
                    options.IncludeAnimatedGifs = true;
                    break;

                case "--skip-thumbnails":
                    options.SkipThumbnails = true;
                    break;

                // JavaScript rendering (the killer feature)
                case "--enable-js":
                    options.EnableJavaScriptRendering = true;
                    break;

                case "--js-wait":
                    if (!TryReadIntOption(args, ref i, "--js-wait", out var jsWait))
                        return null;
                    if (jsWait < 500 || jsWait > 30000)
                    {
                        Console.Error.WriteLine("[ERROR] --js-wait must be between 500 and 30000 milliseconds.");
                        return null;
                    }
                    options.JavaScriptWaitTimeMs = jsWait;
                    break;

                // Checkpoint and logging
                case "--checkpoint":
                    if (!TryReadValueOption(args, ref i, "--checkpoint", out var checkpointFile))
                        return null;
                    options.CheckpointFile = checkpointFile;
                    options.EnableCheckpoints = true;
                    break;

                case "--log":
                    if (!TryReadValueOption(args, ref i, "--log", out var logFile))
                        return null;
                    options.LogFilePath = logFile;
                    options.EnableFileLogging = true;
                    break;

                case "--verbose":
                    options.IsVerbose = true;
                    break;

                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    return null;

                default:
                    Console.Error.WriteLine($"[ERROR] Unknown option: {arg}");
                    return null;
            }
        }

        return options;
    }

    static void ApplyConfigFile(ScraperOptions options, string configFile)
    {
        try
        {
            var config = ConfigFile.Load(configFile);
            if (!string.IsNullOrEmpty(config.OutputFolder))
                options.OutputFolder = config.OutputFolder;
            options.MaxPages = config.MaxPages;
            options.RequestDelayMs = config.RequestDelayMs;
            options.MaxConcurrentDownloads = config.MaxConcurrentDownloads;
            options.MaxDepth = config.MaxDepth;
            options.StealthMode = config.StealthMode;
            options.RespectRobotsTxt = config.RespectRobotsTxt;
            options.IncludeAnimatedGifs = config.IncludeAnimatedGifs;
            if (!string.IsNullOrEmpty(config.ConvertToFormat))
                options.ConvertToFormat = config.ConvertToFormat;
            options.JpegQuality = config.JpegQuality;
            options.OutputOrganization = config.OutputOrganization;
            options.MinImageWidth = config.MinImageWidth;
            options.MinImageHeight = config.MinImageHeight;
            options.MaxImageWidth = config.MaxImageWidth;
            options.MaxImageHeight = config.MaxImageHeight;
            options.MinFileSizeKb = config.MinFileSizeKb;
            options.MaxFileSizeKb = config.MaxFileSizeKb;
            if (!string.IsNullOrEmpty(config.FilenamePattern))
                options.FilenamePattern = config.FilenamePattern;
            if (!string.IsNullOrEmpty(config.UrlRegexFilter))
                options.UrlRegexFilter = config.UrlRegexFilter;
            options.SkipThumbnails = config.SkipThumbnails;
            options.EnableJavaScriptRendering = config.EnableJavaScriptRendering;
            options.JavaScriptWaitTimeMs = config.JavaScriptWaitTimeMs;
            options.EnableFileLogging = config.EnableFileLogging;
            if (!string.IsNullOrEmpty(config.LogFilePath))
                options.LogFilePath = config.LogFilePath;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load config: {ex.Message}");
        }
    }

    static bool IsHelpOption(string arg) =>
        arg.ToLowerInvariant() is "--help" or "-h" or "/?";

    static bool TryReadValueOption(string[] args, ref int index, string optionName, out string value)
    {
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"[ERROR] {optionName} requires a value.");
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    static bool TryReadIntOption(string[] args, ref int index, string optionName, out int value)
    {
        value = 0;

        if (!TryReadValueOption(args, ref index, optionName, out var raw))
            return false;

        if (!int.TryParse(raw, out value))
        {
            Console.Error.WriteLine($"[ERROR] Invalid value for {optionName}. Must be an integer.");
            return false;
        }

        return true;
    }

    static void PrintUsage()
    {
        Console.WriteLine("PixThief - Advanced Image Web Scraper");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  PixThief.exe <url> [options]");
        Console.WriteLine();
        Console.WriteLine("Core Options:");
        Console.WriteLine("  --domain                   Crawl entire domain (not just single page)");
        Console.WriteLine("  --config <file>            Load settings from JSON config file");
        Console.WriteLine();
        Console.WriteLine("Output Options:");
        Console.WriteLine("  --out <folder>             Custom output folder name");
        Console.WriteLine("  --organize <type>          Output organization: flat, by-page, by-date, mirrored");
        Console.WriteLine();
        Console.WriteLine("Crawling Options:");
        Console.WriteLine("  --max-pages <n>            Max pages to crawl (default: 100)");
        Console.WriteLine("  --max-depth <n>            Max crawl depth (default: unlimited)");
        Console.WriteLine("  --robots-txt <bool>        Respect robots.txt (default: true)");
        Console.WriteLine();
        Console.WriteLine("Download Options:");
        Console.WriteLine("  --concurrency <n>          Parallel downloads (1-32, default: 4)");
        Console.WriteLine("  --stealth                  Enable stealth mode with randomized delays");
        Console.WriteLine("  --delay <ms>               Fixed delay between requests (disables randomization)");
        Console.WriteLine();
        Console.WriteLine("Image Filtering:");
        Console.WriteLine("  --min-width <px>           Minimum image width");
        Console.WriteLine("  --min-height <px>          Minimum image height");
        Console.WriteLine("  --max-width <px>           Maximum image width");
        Console.WriteLine("  --max-height <px>          Maximum image height");
        Console.WriteLine("  --min-size <kb>            Minimum file size in KB");
        Console.WriteLine("  --max-size <kb>            Maximum file size in KB");
        Console.WriteLine("  --filename-pattern <regex> Regex pattern for filenames");
        Console.WriteLine("  --url-regex <regex>        Regex pattern for image URLs");
        Console.WriteLine("  --include-gifs             Include animated GIFs");
        Console.WriteLine("  --skip-thumbnails          Skip small thumbnail images (<200x200px)");
        Console.WriteLine();
        Console.WriteLine("Image Conversion:");
        Console.WriteLine("  --convert-to <fmt>         Convert images to: jpg, png, or gif");
        Console.WriteLine("  --jpeg-quality <n>         JPEG quality 1-100 (default: 90)");
        Console.WriteLine();
        Console.WriteLine("Advanced Features:");
        Console.WriteLine("  --enable-js                Enable JavaScript rendering (Playwright)");
        Console.WriteLine("  --js-wait <ms>             JS wait time (500-30000ms, default: 3000)");
        Console.WriteLine("  --checkpoint <file>        Save progress checkpoints");
        Console.WriteLine("  --log <file>               Log all operations to file");
        Console.WriteLine("  --verbose                  Print detailed information");
        Console.WriteLine("  --help                     Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Simple page scrape");
        Console.WriteLine("  PixThief.exe https://example.com");
        Console.WriteLine();
        Console.WriteLine("  # Domain crawl with JS rendering (killer feature!)");
        Console.WriteLine("  PixThief.exe https://example.com --domain --enable-js --stealth");
        Console.WriteLine();
        Console.WriteLine("  # Filtered download with size limits");
        Console.WriteLine("  PixThief.exe https://example.com --min-width 640 --max-size 5000");
        Console.WriteLine();
        Console.WriteLine("  # Parallel downloads with custom organization");
        Console.WriteLine("  PixThief.exe https://example.com --domain --concurrency 8 --organize by-date");
        Console.WriteLine();
        Console.WriteLine("  # With configuration file");
        Console.WriteLine("  PixThief.exe https://example.com --config settings.json");
        Console.WriteLine();
    }

    static string SanitizeFileName(string fileName)
    {
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidRegex = new Regex($"[{invalidChars}]");
        return invalidRegex.Replace(fileName, "_");
    }
}
