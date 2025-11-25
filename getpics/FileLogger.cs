using System.Collections.Concurrent;
using System.Text;

namespace PixThief;

/// <summary>
/// Logs all operations to a file for large crawls
/// </summary>
class FileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly Task _writerTask;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;

    public FileLogger(string? customLogPath = null)
    {
        if (!string.IsNullOrEmpty(customLogPath))
        {
            _logFilePath = customLogPath;
        }
        else
        {
            var cacheDir = PlatformUtilities.GetDefaultCacheDirectory();
            Directory.CreateDirectory(cacheDir);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(cacheDir, $"pixthief_log_{timestamp}.txt");
        }

        _writer = new StreamWriter(
            File.Open(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read),
            Encoding.UTF8,
            bufferSize: 65536)
        {
            AutoFlush = false
        };

        _cancellationTokenSource = new CancellationTokenSource();
        _writerTask = ProcessQueueAsync(_cancellationTokenSource.Token);

        Console.WriteLine($"[INFO] Logging to {_logFilePath}");
    }

    /// <summary>
    /// Log a message
    /// </summary>
    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{timestamp}] {message}";
        _queue.Enqueue(logLine);
    }

    /// <summary>
    /// Log with category
    /// </summary>
    public void Log(string category, string message)
    {
        Log($"[{category}] {message}");
    }

    /// <summary>
    /// Get the log file path
    /// </summary>
    public string GetLogPath() => _logFilePath;

    /// <summary>
    /// Process queued messages in background
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested || !_queue.IsEmpty)
            {
                while (_queue.TryDequeue(out var message))
                {
                    await _writer.WriteLineAsync(message);
                }

                if (!_queue.IsEmpty)
                {
                    await _writer.FlushAsync();
                }

                await Task.Delay(100, cancellationToken);
            }

            await _writer.FlushAsync();
        }
        catch (OperationCanceledException)
        {
            // Flush remaining items
            while (_queue.TryDequeue(out var message))
            {
                await _writer.WriteLineAsync(message);
            }
            await _writer.FlushAsync();
        }
    }

    /// <summary>
    /// Flush and close the logger
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource.Cancel();

        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch { }

        _writer?.Flush();
        _writer?.Dispose();
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
