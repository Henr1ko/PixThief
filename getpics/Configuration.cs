using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixThief;

/// <summary>
/// Configuration file support for saving and loading preferred settings
/// </summary>
class ConfigFile
{
    public string? OutputFolder { get; set; }
    public int MaxPages { get; set; } = 100;
    public int RequestDelayMs { get; set; } = 1000;
    public int MaxConcurrentDownloads { get; set; } = 4;
    public int MaxDepth { get; set; } = -1; // -1 = unlimited
    public bool StealthMode { get; set; }
    public bool RespectRobotsTxt { get; set; } = true;
    public bool IncludeAnimatedGifs { get; set; }
    public string? ConvertToFormat { get; set; }
    public int JpegQuality { get; set; } = 90;
    public string OutputOrganization { get; set; } = "flat"; // "flat", "by-page", "by-date", "mirrored"

    // Filtering options
    public int MinImageWidth { get; set; } = 0;
    public int MinImageHeight { get; set; } = 0;
    public int MaxImageWidth { get; set; } = 0; // 0 = unlimited
    public int MaxImageHeight { get; set; } = 0; // 0 = unlimited
    public int MinFileSizeKb { get; set; } = 0;
    public int MaxFileSizeKb { get; set; } = 0; // 0 = unlimited
    public string? FilenamePattern { get; set; } // Regex pattern for filenames to match
    public string? UrlRegexFilter { get; set; } // Regex pattern for image URLs to match
    public bool SkipThumbnails { get; set; } // Skip small thumbnail images

    // JavaScript rendering
    public bool EnableJavaScriptRendering { get; set; }
    public int JavaScriptWaitTimeMs { get; set; } = 3000;

    // Logging
    public bool EnableFileLogging { get; set; }
    public string? LogFilePath { get; set; }

    /// <summary>
    /// Load configuration from a JSON file
    /// </summary>
    public static ConfigFile Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new ConfigFile(); // Return defaults if file doesn't exist

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            return JsonSerializer.Deserialize<ConfigFile>(json, options) ?? new ConfigFile();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load config file: {ex.Message}. Using defaults.");
            return new ConfigFile();
        }
    }

    /// <summary>
    /// Save configuration to a JSON file
    /// </summary>
    public void Save(string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
            Console.WriteLine($"[INFO] Configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to save config file: {ex.Message}");
        }
    }
}
