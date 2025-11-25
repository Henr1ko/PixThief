using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixThief;

/// <summary>
/// Manages checkpoint/resume functionality for interrupted crawls
/// </summary>
class CheckpointManager
{
    public class CrawlCheckpoint
    {
        public string Url { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<string> VisitedUrls { get; set; } = new();
        public List<string> DownloadedImages { get; set; } = new();
        public HashSet<string> DownloadedHashes { get; set; } = new();
        public int PagesProcessed { get; set; }
        public int ImagesFound { get; set; }
        public int ImagesDownloaded { get; set; }
        public int ImagesFailed { get; set; }
        public long BytesDownloaded { get; set; }
        public Dictionary<string, string> ScraperOptionsJson { get; set; } = new();
    }

    private readonly string _checkpointFile;
    private CrawlCheckpoint? _currentCheckpoint;

    public CheckpointManager(string? customCheckpointFile = null)
    {
        if (!string.IsNullOrEmpty(customCheckpointFile))
        {
            _checkpointFile = customCheckpointFile;
        }
        else
        {
            var cacheDir = PlatformUtilities.GetDefaultCacheDirectory();
            Directory.CreateDirectory(cacheDir);
            _checkpointFile = Path.Combine(cacheDir, "last_checkpoint.json");
        }
    }

    /// <summary>
    /// Create a new checkpoint
    /// </summary>
    public void CreateCheckpoint(string url, Dictionary<string, string> options)
    {
        _currentCheckpoint = new CrawlCheckpoint
        {
            Url = url,
            ScraperOptionsJson = options,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SaveCheckpoint();
    }

    /// <summary>
    /// Load existing checkpoint if it exists
    /// </summary>
    public CrawlCheckpoint? LoadCheckpoint()
    {
        if (!File.Exists(_checkpointFile))
            return null;

        try
        {
            var json = File.ReadAllText(_checkpointFile);
            _currentCheckpoint = JsonSerializer.Deserialize(json, CheckpointContext.Default.CrawlCheckpoint);
            return _currentCheckpoint;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load checkpoint: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update checkpoint with progress
    /// </summary>
    public void UpdateCheckpoint(HashSet<string> visitedUrls, HashSet<string> downloadedImages,
        HashSet<string> downloadedHashes, int pagesProcessed, int imagesFound,
        int imagesDownloaded, int imagesFailed, long bytesDownloaded)
    {
        if (_currentCheckpoint == null)
            return;

        _currentCheckpoint.VisitedUrls = new List<string>(visitedUrls);
        _currentCheckpoint.DownloadedImages = new List<string>(downloadedImages);
        _currentCheckpoint.DownloadedHashes = new HashSet<string>(downloadedHashes);
        _currentCheckpoint.PagesProcessed = pagesProcessed;
        _currentCheckpoint.ImagesFound = imagesFound;
        _currentCheckpoint.ImagesDownloaded = imagesDownloaded;
        _currentCheckpoint.ImagesFailed = imagesFailed;
        _currentCheckpoint.BytesDownloaded = bytesDownloaded;
        _currentCheckpoint.UpdatedAt = DateTime.UtcNow;

        SaveCheckpoint();
    }

    /// <summary>
    /// Save checkpoint to disk
    /// </summary>
    public void SaveCheckpoint()
    {
        if (_currentCheckpoint == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(_currentCheckpoint, CheckpointContext.Default.CrawlCheckpoint);
            File.WriteAllText(_checkpointFile, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to save checkpoint: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete checkpoint (after successful completion)
    /// </summary>
    public void DeleteCheckpoint()
    {
        try
        {
            if (File.Exists(_checkpointFile))
                File.Delete(_checkpointFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to delete checkpoint: {ex.Message}");
        }
    }
}

[JsonSerializable(typeof(CheckpointManager.CrawlCheckpoint))]
internal partial class CheckpointContext : JsonSerializerContext
{
}
