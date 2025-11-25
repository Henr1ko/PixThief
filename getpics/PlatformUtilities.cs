using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PixThief;

/// <summary>
/// Platform-specific utilities for cross-platform support (Windows, Linux, macOS)
/// </summary>
class PlatformUtilities
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Get platform-appropriate path separator
    /// </summary>
    public static char PathSeparator => Path.DirectorySeparatorChar;

    /// <summary>
    /// Get default config file location (OS-specific)
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        if (IsWindows)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "PixThief", "config.json");
        }
        else if (IsLinux || IsMacOS)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "pixthief", "config.json");
        }

        return "pixthief.config.json";
    }

    /// <summary>
    /// Get default cache directory (for storing state, checkpoints, etc.)
    /// </summary>
    public static string GetDefaultCacheDirectory()
    {
        if (IsWindows)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PixThief", "cache");
        }
        else if (IsLinux || IsMacOS)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".cache", "pixthief");
        }

        return ".pixthief_cache";
    }

    /// <summary>
    /// Normalize path for current platform
    /// </summary>
    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Calculate SHA256 hash of file content for duplicate detection
    /// </summary>
    public static string CalculateFileHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Get file size in a human-readable format
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            < KB => $"{bytes} B",
            < MB => $"{bytes / (double)KB:F2} KB",
            < GB => $"{bytes / (double)MB:F2} MB",
            _ => $"{bytes / (double)GB:F2} GB"
        };
    }

    /// <summary>
    /// Convert file size from KB to bytes
    /// </summary>
    public static long ConvertKbToBytes(int kb)
    {
        return (long)kb * 1024;
    }
}
