namespace PixThief;

/// <summary>
/// Tracks progress statistics for the TUI
/// </summary>
class ProgressStats
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    public string Url { get; set; } = string.Empty;
    public int PagesFound { get; set; }
    public int PagesCrawled { get; set; }
    public int ImagesFound { get; set; }
    public int ImagesDownloaded { get; set; }
    public int ImagesSkipped { get; set; }
    public int ImagesFailed { get; set; }
    public long BytesDownloaded { get; set; }
    public HashSet<string> CurrentlyDownloading { get; set; } = new();
    public List<string> RecentActions { get; set; } = new();

    public TimeSpan ElapsedTime => DateTime.UtcNow - _startTime;

    public double DownloadSpeed
    {
        get
        {
            var seconds = ElapsedTime.TotalSeconds;
            if (seconds <= 0) return 0;
            return BytesDownloaded / seconds;
        }
    }

    public int EstimatedSecondsRemaining
    {
        get
        {
            if (ImagesDownloaded + ImagesFailed == 0) return 0;
            var avgTimePerImage = ElapsedTime.TotalSeconds / (ImagesDownloaded + ImagesFailed);
            var remaining = ImagesFound - ImagesDownloaded - ImagesFailed;
            return (int)(avgTimePerImage * remaining);
        }
    }

    public double ProgressPercent
    {
        get
        {
            if (ImagesFound == 0) return 0;
            return ((ImagesDownloaded + ImagesFailed) / (double)ImagesFound) * 100;
        }
    }

    /// <summary>
    /// Add an activity log entry (keep last 10)
    /// </summary>
    public void AddAction(string action)
    {
        RecentActions.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {action}");
        if (RecentActions.Count > 10)
            RecentActions.RemoveAt(RecentActions.Count - 1);
    }

    /// <summary>
    /// Get a summary of current stats
    /// </summary>
    public string GetSummary()
    {
        var speed = FormatBytes(DownloadSpeed) + "/s";
        var elapsed = $"{(int)ElapsedTime.TotalHours:D2}:{(int)ElapsedTime.Minutes:D2}:{(int)ElapsedTime.Seconds:D2}";
        return $"Pages: {PagesCrawled}/{PagesFound} | Images: {ImagesDownloaded}/{ImagesFound} " +
               $"| Failed: {ImagesFailed} | Skipped: {ImagesSkipped} " +
               $"| Downloaded: {FormatBytes(BytesDownloaded)} | Speed: {speed} | Time: {elapsed}";
    }

    private static string FormatBytes(double bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            < KB => $"{bytes:F0} B",
            < MB => $"{bytes / KB:F2} KB",
            < GB => $"{bytes / MB:F2} MB",
            _ => $"{bytes / GB:F2} GB"
        };
    }
}
