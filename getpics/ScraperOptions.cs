namespace PixThief;

class ScraperOptions
{
    public string? Url { get; set; }
    public bool IsDomainMode { get; set; }
    public string? OutputFolder { get; set; }
    public int MaxPages { get; set; } = 100;
    public bool IsVerbose { get; set; }
    public bool IncludeAnimatedGifs { get; set; }
    public bool StealthMode { get; set; }
    public bool RandomizeDelays { get; set; } // Only true for --stealth, false for --delay
    
    // Research-backed safe default: 1000ms (1 second)
    // - Avoids rate limiting (most sites allow 1-2 req/sec)
    // - Prevents IP bans
    // - Bypasses basic bot detection
    // - With �40% randomization: 600-1400ms range
    public int RequestDelayMs { get; set; } = 1000;
    
    // Image conversion options
    public string? ConvertToFormat { get; set; } // "jpg", "png", or "gif"
    public int JpegQuality { get; set; } = 90; // Quality for JPEG conversion (1-100)

    // Parallel download options
    public int MaxConcurrentDownloads { get; set; } = 4;

    // Depth control for domain crawling
    public int MaxDepth { get; set; } = -1; // -1 = unlimited

    // robots.txt respect toggle
    public bool RespectRobotsTxt { get; set; } = true;

    // JavaScript rendering support
    public bool EnableJavaScriptRendering { get; set; }
    public int JavaScriptWaitTimeMs { get; set; } = 3000;

    // Output organization options: "flat", "by-page", "by-date", "mirrored"
    public string OutputOrganization { get; set; } = "flat";

    // Filtering options
    public int MinImageWidth { get; set; }
    public int MinImageHeight { get; set; }
    public int MaxImageWidth { get; set; } // 0 = unlimited
    public int MaxImageHeight { get; set; } // 0 = unlimited
    public int MinFileSizeKb { get; set; }
    public int MaxFileSizeKb { get; set; } // 0 = unlimited
    public string? FilenamePattern { get; set; } // Regex pattern for filenames
    public string? UrlRegexFilter { get; set; } // Regex pattern for image URLs
    public bool SkipThumbnails { get; set; } // Skip small thumbnail images

    // Resume/checkpoint capability
    public bool EnableCheckpoints { get; set; } = true;
    public string? CheckpointFile { get; set; }

    // Logging
    public bool EnableFileLogging { get; set; }
    public string? LogFilePath { get; set; }

    // Configuration file
    public string? ConfigFile { get; set; }
}
