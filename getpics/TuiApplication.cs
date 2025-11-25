using Spectre.Console;
using System.Diagnostics;

namespace PixThief;

/// <summary>
/// Complete TUI-based PixThief application that handles everything from startup
/// No CLI arguments, pure TUI-driven interface with all functionality
/// </summary>
class TuiApplication
{
    private ScraperOptions _options = new();
    private ProgressStats? _stats;
    private ImageScraperEnhanced? _scraper;
    private bool _running = true;

    public TuiApplication()
    {
        // Enable file logging by default for debugging
        _options.EnableFileLogging = true;
        _options.LogFilePath = "PixThief_Debug.log";
    }

    public async Task RunAsync()
    {
        try
        {
            Console.Title = "PixThief - Advanced Image Scraper";
            
            // Show splash screen
            ShowSplashScreen();

            while (_running)
            {
                AnsiConsole.Clear();
                DrawHeader("Main Menu");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]What would you like to do?[/]")
                        .PageSize(10)
                        .AddChoices(new[] {
                            "Set URL & Mode",
                            "Download Settings",
                            "Filtering Settings",
                            "Output Settings",
                            "Advanced Settings",
                            "View Current Settings",
                            "Start Download",
                            "Exit"
                        }));

                switch (choice)
                {
                    case "Set URL & Mode":
                        ShowUrlInput();
                        break;
                    case "Download Settings":
                        ShowDownloadSettings();
                        break;
                    case "Filtering Settings":
                        ShowFilteringSettings();
                        break;
                    case "Output Settings":
                        ShowOutputSettings();
                        break;
                    case "Advanced Settings":
                        ShowAdvancedSettings();
                        break;
                    case "View Current Settings":
                        ShowViewSettings();
                        break;
                    case "Start Download":
                        await ValidateAndDownload();
                        break;
                    case "Exit":
                        _running = false;
                        break;
                }
            }

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[cyan]Thanks for using PixThief! Goodbye![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            WaitForKey();
        }
    }

    private void ShowSplashScreen()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("PixThief")
                .Color(Color.Cyan1));
        
        AnsiConsole.MarkupLine("[dim]v2.0 - The Ultimate Image Scraper[/]");
        AnsiConsole.WriteLine();
        
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .Start("Initializing...", ctx => 
            {
                Thread.Sleep(1000);
                ctx.Status("Ready!");
                Thread.Sleep(500);
            });
    }

    private void ShowUrlInput()
    {
        AnsiConsole.Clear();
        DrawHeader("URL & Mode Configuration");

        var url = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Enter URL (HTTP/HTTPS):[/]")
                .Validate(input => 
                {
                    if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return ValidationResult.Error("[red]Invalid URL. Must be HTTP or HTTPS.[/]");
                    }
                    return ValidationResult.Success();
                }));

        _options.Url = url;

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select Crawl Mode:[/]")
                .AddChoices(new[] {
                    "Single Page",
                    "Entire Domain"
                }));

        _options.IsDomainMode = mode == "Entire Domain";

        AnsiConsole.MarkupLine($"[green]URL set to: {_options.Url}[/]");
        AnsiConsole.MarkupLine($"[green]Mode: {mode}[/]");
        WaitForKey();
    }

    private void ShowDownloadSettings()
    {
        while (true)
        {
            AnsiConsole.Clear();
            DrawHeader("Download Settings");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Setting[/]")
                .AddColumn("[cyan]Current Value[/]");

            table.AddRow("Concurrency", $"{_options.MaxConcurrentDownloads} threads");
            table.AddRow("Stealth Mode", _options.StealthMode ? "[green]Enabled[/]" : "[dim]Disabled[/]");
            table.AddRow("Delay Mode", _options.RandomizeDelays ? "Randomized" : $"Fixed ({_options.RequestDelayMs}ms)");
            table.AddRow("Max Pages", _options.MaxPages.ToString());

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select setting to change:[/]")
                    .AddChoices(new[] {
                        "Set Concurrency",
                        "Toggle Stealth Mode",
                        "Set Delay",
                        "Set Max Pages",
                        "Back"
                    }));

            if (choice == "Back") return;

            switch (choice)
            {
                case "Set Concurrency":
                    _options.MaxConcurrentDownloads = AnsiConsole.Prompt(
                        new TextPrompt<int>("[cyan]Enter concurrency (1-32):[/]")
                            .Validate(n => n is >= 1 and <= 32 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 32")));
                    break;

                case "Toggle Stealth Mode":
                    _options.StealthMode = !_options.StealthMode;
                    _options.RandomizeDelays = _options.StealthMode;
                    break;

                case "Set Delay":
                    var delayType = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[cyan]Select delay type:[/]")
                            .AddChoices(new[] { "Randomized (Stealth)", "Fixed" }));
                    
                    if (delayType == "Randomized (Stealth)")
                    {
                        _options.RandomizeDelays = true;
                        _options.StealthMode = true;
                    }
                    else
                    {
                        _options.RequestDelayMs = AnsiConsole.Prompt(
                            new TextPrompt<int>("[cyan]Enter delay (ms):[/]")
                                .Validate(n => n >= 0 
                                    ? ValidationResult.Success() 
                                    : ValidationResult.Error("Must be positive")));
                        _options.RandomizeDelays = false;
                    }
                    break;

                case "Set Max Pages":
                    _options.MaxPages = AnsiConsole.Prompt(
                        new TextPrompt<int>("[cyan]Enter max pages:[/]")
                            .Validate(n => n > 0 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be positive")));
                    break;
            }
        }
    }

    private void ShowFilteringSettings()
    {
        while (true)
        {
            AnsiConsole.Clear();
            DrawHeader("Filtering Settings");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Setting[/]")
                .AddColumn("[cyan]Current Value[/]");

            table.AddRow("Min Width", _options.MinImageWidth > 0 ? $"{_options.MinImageWidth}px" : "None");
            table.AddRow("Max Width", _options.MaxImageWidth > 0 ? $"{_options.MaxImageWidth}px" : "None");
            table.AddRow("Min Height", _options.MinImageHeight > 0 ? $"{_options.MinImageHeight}px" : "None");
            table.AddRow("Max Height", _options.MaxImageHeight > 0 ? $"{_options.MaxImageHeight}px" : "None");
            table.AddRow("Min File Size", _options.MinFileSizeKb > 0 ? $"{_options.MinFileSizeKb}KB" : "None");
            table.AddRow("Max File Size", _options.MaxFileSizeKb > 0 ? $"{_options.MaxFileSizeKb}KB" : "None");
            table.AddRow("Skip Thumbnails", _options.SkipThumbnails ? "[green]Enabled[/]" : "[dim]Disabled[/]");
            table.AddRow("Include GIFs", _options.IncludeAnimatedGifs ? "[green]Yes[/]" : "[dim]No[/]");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select setting to change:[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "Set Min Width",
                        "Set Max Width",
                        "Set Min Height",
                        "Set Max Height",
                        "Set Min File Size",
                        "Set Max File Size",
                        "Toggle Skip Thumbnails",
                        "Toggle Include GIFs",
                        "Back"
                    }));

            if (choice == "Back") return;

            switch (choice)
            {
                case "Set Min Width":
                    _options.MinImageWidth = PromptInt("Min width (0 for none):");
                    break;
                case "Set Max Width":
                    _options.MaxImageWidth = PromptInt("Max width (0 for none):");
                    break;
                case "Set Min Height":
                    _options.MinImageHeight = PromptInt("Min height (0 for none):");
                    break;
                case "Set Max Height":
                    _options.MaxImageHeight = PromptInt("Max height (0 for none):");
                    break;
                case "Set Min File Size":
                    _options.MinFileSizeKb = PromptInt("Min size KB (0 for none):");
                    break;
                case "Set Max File Size":
                    _options.MaxFileSizeKb = PromptInt("Max size KB (0 for none):");
                    break;
                case "Toggle Skip Thumbnails":
                    _options.SkipThumbnails = !_options.SkipThumbnails;
                    break;
                case "Toggle Include GIFs":
                    _options.IncludeAnimatedGifs = !_options.IncludeAnimatedGifs;
                    break;
            }
        }
    }

    private void ShowOutputSettings()
    {
        while (true)
        {
            AnsiConsole.Clear();
            DrawHeader("Output Settings");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Setting[/]")
                .AddColumn("[cyan]Current Value[/]");

            table.AddRow("Output Folder", _options.OutputFolder ?? "Default (PixThief_Downloads)");
            table.AddRow("Organization", _options.OutputOrganization);
            table.AddRow("Convert Format", _options.ConvertToFormat ?? "None");
            table.AddRow("JPEG Quality", _options.JpegQuality.ToString());

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select setting to change:[/]")
                    .AddChoices(new[] {
                        "Set Output Folder",
                        "Set Organization Mode",
                        "Set Image Format",
                        "Set JPEG Quality",
                        "Back"
                    }));

            if (choice == "Back") return;

            switch (choice)
            {
                case "Set Output Folder":
                    var folder = AnsiConsole.Ask<string>("[cyan]Folder name (leave empty for default):[/]", "");
                    _options.OutputFolder = string.IsNullOrWhiteSpace(folder) ? null : folder;
                    break;

                case "Set Organization Mode":
                    _options.OutputOrganization = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[cyan]Select organization mode:[/]")
                            .AddChoices(new[] { "flat", "by-page", "by-date", "mirrored" }));
                    break;

                case "Set Image Format":
                    var fmt = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[cyan]Select target format:[/]")
                            .AddChoices(new[] { "None (original)", "jpg", "png", "gif" }));
                    
                    _options.ConvertToFormat = fmt == "None (original)" ? null : fmt;
                    break;

                case "Set JPEG Quality":
                    _options.JpegQuality = AnsiConsole.Prompt(
                        new TextPrompt<int>("[cyan]JPEG quality (1-100):[/]")
                            .Validate(n => n is >= 1 and <= 100 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 100")));
                    break;
            }
        }
    }

    private void ShowAdvancedSettings()
    {
        while (true)
        {
            AnsiConsole.Clear();
            DrawHeader("Advanced Settings");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Setting[/]")
                .AddColumn("[cyan]Current Value[/]");

            table.AddRow("JavaScript Rendering", _options.EnableJavaScriptRendering ? "[green]Enabled[/]" : "[dim]Disabled[/]");
            table.AddRow("JS Wait Time", $"{_options.JavaScriptWaitTimeMs}ms");
            table.AddRow("Respect robots.txt", _options.RespectRobotsTxt ? "[green]Yes[/]" : "[red]No[/]");
            table.AddRow("Max Depth", _options.MaxDepth < 0 ? "Unlimited" : _options.MaxDepth.ToString());
            table.AddRow("File Logging", _options.EnableFileLogging ? "[green]Enabled[/]" : "[dim]Disabled[/]");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select setting to change:[/]")
                    .AddChoices(new[] {
                        "Toggle JavaScript Rendering",
                        "Set JS Wait Time",
                        "Toggle Respect robots.txt",
                        "Set Max Depth",
                        "Toggle File Logging",
                        "Back"
                    }));

            if (choice == "Back") return;

            switch (choice)
            {
                case "Toggle JavaScript Rendering":
                    _options.EnableJavaScriptRendering = !_options.EnableJavaScriptRendering;
                    break;

                case "Set JS Wait Time":
                    _options.JavaScriptWaitTimeMs = AnsiConsole.Prompt(
                        new TextPrompt<int>("[cyan]Wait time (500-30000ms):[/]")
                            .Validate(n => n is >= 500 and <= 30000 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 500 and 30000")));
                    break;

                case "Toggle Respect robots.txt":
                    _options.RespectRobotsTxt = !_options.RespectRobotsTxt;
                    break;

                case "Set Max Depth":
                    _options.MaxDepth = AnsiConsole.Prompt(
                        new TextPrompt<int>("[cyan]Max depth (-1 for unlimited):[/]")
                            .Validate(n => n >= -1 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be >= -1")));
                    break;

                case "Toggle File Logging":
                    _options.EnableFileLogging = !_options.EnableFileLogging;
                    if (_options.EnableFileLogging && string.IsNullOrEmpty(_options.LogFilePath))
                    {
                        _options.LogFilePath = AnsiConsole.Ask<string>("[cyan]Log file path:[/]", "PixThief.log");
                    }
                    break;
            }
        }
    }

    private void ShowViewSettings()
    {
        AnsiConsole.Clear();
        DrawHeader("Current Configuration");

        var table = new Table()
            .Border(TableBorder.Double)
            .Title("[cyan bold]All Settings[/]")
            .AddColumn("[yellow]Category[/]")
            .AddColumn("[yellow]Setting[/]")
            .AddColumn("[yellow]Value[/]");

        // Core
        table.AddRow("Core", "URL", _options.Url ?? "[red]Not set[/]");
        table.AddRow("", "Mode", _options.IsDomainMode ? "Domain Crawl" : "Single Page");

        // Download
        table.AddRow("Download", "Concurrency", $"{_options.MaxConcurrentDownloads} threads");
        table.AddRow("", "Stealth Mode", _options.StealthMode ? "[green]Yes[/]" : "[dim]No[/]");
        table.AddRow("", "Delay", _options.RandomizeDelays ? "Randomized" : $"{_options.RequestDelayMs}ms");
        table.AddRow("", "Max Pages", _options.MaxPages.ToString());

        // Filtering
        table.AddRow("Filtering", "Min Width", _options.MinImageWidth > 0 ? $"{_options.MinImageWidth}px" : "None");
        table.AddRow("", "Max Width", _options.MaxImageWidth > 0 ? $"{_options.MaxImageWidth}px" : "None");
        table.AddRow("", "Min Height", _options.MinImageHeight > 0 ? $"{_options.MinImageHeight}px" : "None");
        table.AddRow("", "Max Height", _options.MaxImageHeight > 0 ? $"{_options.MaxImageHeight}px" : "None");
        table.AddRow("", "Min Size", _options.MinFileSizeKb > 0 ? $"{_options.MinFileSizeKb}KB" : "None");
        table.AddRow("", "Max Size", _options.MaxFileSizeKb > 0 ? $"{_options.MaxFileSizeKb}KB" : "None");
        table.AddRow("", "Skip Thumbnails", _options.SkipThumbnails ? "[green]Yes[/]" : "[dim]No[/]");
        table.AddRow("", "Include GIFs", _options.IncludeAnimatedGifs ? "[green]Yes[/]" : "[dim]No[/]");

        // Output
        table.AddRow("Output", "Folder", _options.OutputFolder ?? "Default");
        table.AddRow("", "Organization", _options.OutputOrganization);
        table.AddRow("", "Format", _options.ConvertToFormat ?? "Original");
        table.AddRow("", "JPEG Quality", _options.JpegQuality.ToString());

        // Advanced
        table.AddRow("Advanced", "JS Rendering", _options.EnableJavaScriptRendering ? "[green]Enabled[/]" : "[dim]Disabled[/]");
        table.AddRow("", "JS Wait Time", $"{_options.JavaScriptWaitTimeMs}ms");
        table.AddRow("", "Robots.txt", _options.RespectRobotsTxt ? "[green]Respect[/]" : "[red]Ignore[/]");
        table.AddRow("", "Max Depth", _options.MaxDepth < 0 ? "Unlimited" : _options.MaxDepth.ToString());
        table.AddRow("", "File Logging", _options.EnableFileLogging ? "[green]Enabled[/]" : "[dim]Disabled[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        WaitForKey();
    }

    private async Task ValidateAndDownload()
    {
        if (string.IsNullOrEmpty(_options.Url))
        {
            AnsiConsole.MarkupLine("[red]Please set a URL first![/]");
            WaitForKey();
            return;
        }

        AnsiConsole.Clear();
        DrawHeader("Downloading Images");

        _stats = new ProgressStats { Url = _options.Url ?? "Unknown" };
        _scraper = new ImageScraperEnhanced(_options, _stats);

        // Create download dashboard
        var dashboard = new DownloadDashboard(_stats);
        string? errorMessage = null;

        try
        {
            // Start the dashboard task
            var dashboardTask = dashboard.RunAsync();
            
            // Start the download task
            Task downloadTask;
            if (_options.IsDomainMode)
            {
                downloadTask = _scraper.DownloadImagesForDomainAsync();
            }
            else
            {
                downloadTask = _scraper.DownloadImagesForSinglePageAsync();
            }

            // Wait for download to finish
            await downloadTask;
            
            // Stop dashboard
            dashboard.Stop();
            await dashboardTask;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            dashboard.Stop();
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            AnsiConsole.Clear();
            DrawHeader("Download Error");
            AnsiConsole.MarkupLine($"[red]{errorMessage}[/]");
            AnsiConsole.WriteLine();
            WaitForKey();
        }
        else
        {
            ShowCompletionSummary();
        }
    }

    private void ShowCompletionSummary()
    {
        if (_stats == null) return;

        AnsiConsole.Clear();
        DrawHeader("Download Complete");

        var summaryTable = new Table()
            .Border(TableBorder.Double)
            .Title("[green bold]Summary Statistics[/]")
            .AddColumn("[cyan]Metric[/]")
            .AddColumn("[cyan]Value[/]");

        summaryTable.AddRow("Pages Crawled", _stats.PagesCrawled.ToString());
        summaryTable.AddRow("Images Found", _stats.ImagesFound.ToString());
        summaryTable.AddRow("Downloaded", $"[green]{_stats.ImagesDownloaded}[/]");
        summaryTable.AddRow("Skipped", $"[yellow]{_stats.ImagesSkipped}[/]");
        summaryTable.AddRow("Failed", $"[red]{_stats.ImagesFailed}[/]");
        summaryTable.AddRow("Total Data", FormatBytes(_stats.BytesDownloaded));
        summaryTable.AddRow("Avg Speed", $"{FormatBytes(_stats.DownloadSpeed)}/s");
        summaryTable.AddRow("Total Time", FormatTime(_stats.ElapsedTime));

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        WaitForKey();
    }

    private void DrawHeader(string title)
    {
        var rule = new Rule($"[cyan]{title}[/]");
        rule.Style = Style.Parse("cyan dim");
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    private int PromptInt(string prompt)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<int>($"[cyan]{prompt}[/]")
                .Validate(n => n >= 0 
                    ? ValidationResult.Success() 
                    : ValidationResult.Error("Must be non-negative")));
    }

    private void WaitForKey()
    {
        AnsiConsole.Markup("[yellow]Press Enter to continue...[/]");
        Console.ReadLine();
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
