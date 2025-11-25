using Spectre.Console;
using System.Diagnostics;

namespace PixThief;

/// <summary>
/// Beautiful, clean download dashboard that uses AnsiConsole.Live for flicker-free updates
/// </summary>
class DownloadDashboard
{
    private readonly ProgressStats _stats;
    private bool _isRunning;
    private CancellationTokenSource _cancellationTokenSource = new();

    public DownloadDashboard(ProgressStats stats)
    {
        _stats = stats;
    }

    public async Task RunAsync()
    {
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        // Create the layout
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content")
                    .SplitColumns(
                        new Layout("Stats").Ratio(6),
                        new Layout("Log").Ratio(4)
                    ),
                new Layout("Footer").Size(3)
            );

        // Run the live display
        await AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        UpdateLayout(layout);
                        ctx.Refresh();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash - keep the UI updating
                        System.Diagnostics.Debug.WriteLine($"Dashboard update error: {ex.Message}");
                    }
                    await Task.Delay(100); // 10Hz refresh rate
                }
            });
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
    }

    private void UpdateLayout(Layout layout)
    {
        // Header
        layout["Header"].Update(
            new Panel(
                new Markup($"[bold cyan]PixThief - Downloading...[/] [dim]({_stats.Url})[/]")
            ).Border(BoxBorder.Double).Expand());

        // Stats Panel
        var statsTable = new Table().Border(TableBorder.None).Expand();
        statsTable.AddColumn("Metric");
        statsTable.AddColumn("Value");
        statsTable.AddColumn("Status");

        statsTable.AddRow(
            "Pages Crawled", 
            $"{_stats.PagesCrawled}/{_stats.PagesFound}", 
            _stats.PagesCrawled > 0 ? "[green]Active[/]" : "[dim]Waiting[/]");
        
        statsTable.AddRow(
            "Images Downloaded", 
            $"[green]{_stats.ImagesDownloaded}[/]", 
            _stats.ImagesDownloaded > 0 ? "[green]Saving[/]" : "[dim]Waiting[/]");
        
        statsTable.AddRow(
            "Skipped / Failed", 
            $"[yellow]{_stats.ImagesSkipped}[/] / [red]{_stats.ImagesFailed}[/]", 
            _stats.ImagesFailed > 0 ? "[red]Errors[/]" : "[green]OK[/]");

        statsTable.AddRow(
            "Data / Speed", 
            $"{FormatBytes(_stats.BytesDownloaded)} / {FormatBytes(_stats.DownloadSpeed)}/s", 
            "[blue]Network[/]");

        statsTable.AddRow(
            "Time Elapsed", 
            FormatTime(_stats.ElapsedTime), 
            "[dim]Running[/]");

        // Progress Bar
        var progressBar = new BarChart()
            .Width(40)
            .Label("[cyan]Overall Progress[/]")
            .AddItem("Progress", _stats.ProgressPercent, Color.Cyan1);

        var statsContent = new Rows(
            statsTable,
            new Rule(),
            progressBar
        );

        layout["Stats"].Update(
            new Panel(statsContent)
                .Header("Statistics")
                .Border(BoxBorder.Rounded)
                .Expand());

        // Log Panel
        var logRows = new List<Markup>();
        var recentActions = _stats.RecentActions.ToList(); // Thread-safe snapshot
        foreach (var action in recentActions.Take(12))
        {
            var escaped = action.Replace("[", "[[").Replace("]", "]]");
            logRows.Add(new Markup($"[dim]{escaped}[/]"));
        }

        if (logRows.Count == 0)
        {
            logRows.Add(new Markup("[dim]Waiting for activity...[/]"));
        }

        layout["Log"].Update(
            new Panel(new Rows(logRows))
                .Header("Recent Activity")
                .Border(BoxBorder.Rounded)
                .Expand());

        // Footer
        var currentDownloads = _stats.CurrentlyDownloading.ToList(); // Thread-safe snapshot
        var currentItem = currentDownloads.Count > 0 ? currentDownloads.First() : "Waiting...";
        layout["Footer"].Update(
            new Panel(
                new Markup($"[dim]Currently downloading: {currentItem}[/]")
            ).Border(BoxBorder.None));
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

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m {time.Seconds}s";
        if (time.TotalMinutes >= 1)
            return $"{(int)time.TotalMinutes}m {time.Seconds}s";
        return $"{(int)time.TotalSeconds}s";
    }
}
