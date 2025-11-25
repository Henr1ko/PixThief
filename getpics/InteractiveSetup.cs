using Spectre.Console;
using System.Text.RegularExpressions;

namespace PixThief;

/// <summary>
/// Interactive TUI setup menu for configuring scraper options before crawl starts
/// </summary>
class InteractiveSetup
{
    private readonly ScraperOptions _options;

    public InteractiveSetup(ScraperOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Show interactive setup menu
    /// </summary>
    public void ShowSetupMenu()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.WriteLine();

        // Always show URL and domain mode (already set from CLI)
        AnsiConsole.MarkupLine($"[cyan]URL:[/] {_options.Url}");
        AnsiConsole.MarkupLine($"[cyan]Mode:[/] {(_options.IsDomainMode ? "Domain Crawl" : "Single Page")}");
        AnsiConsole.WriteLine();

        // Show settings menu
        while (true)
        {
            AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[bold cyan]     PIXTHIEF CONFIGURATION MENU[/]");
            AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════[/]");
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Select option to configure:[/]")
                    .AddChoices(new[]
                    {
                        "1. Download Settings",
                        "2. Filtering Options",
                        "3. Output Settings",
                        "4. Advanced Features",
                        "5. View Current Settings",
                        "6. Start Crawl ➜"
                    }));

            switch (choice[0])
            {
                case '1':
                    ConfigureDownloadSettings();
                    break;
                case '2':
                    ConfigureFilteringOptions();
                    break;
                case '3':
                    ConfigureOutputSettings();
                    break;
                case '4':
                    ConfigureAdvancedFeatures();
                    break;
                case '5':
                    ViewCurrentSettings();
                    break;
                case '6':
                    AnsiConsole.Clear();
                    return;
            }
        }
    }

    private void ConfigureDownloadSettings()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.MarkupLine("[bold yellow]DOWNLOAD SETTINGS[/]");
        AnsiConsole.WriteLine();

        var options = new List<string>
        {
            $"[cyan]Concurrent Downloads:[/] {_options.MaxConcurrentDownloads} [dim](1-32)[/]",
            $"[cyan]Stealth Mode:[/] {(_options.StealthMode ? "✓ Enabled" : "✗ Disabled")}",
            $"[cyan]Request Delay:[/] {_options.RequestDelayMs}ms",
            $"[cyan]Max Pages:[/] {_options.MaxPages}",
            "Back to Menu"
        };

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select setting:")
                .AddChoices(options));

        if (choice.Contains("Concurrent"))
        {
            var concurrency = AnsiConsole.Prompt(
                new TextPrompt<int>($"[cyan]Enter concurrent downloads (1-32):[/] ")
                    .DefaultValue(_options.MaxConcurrentDownloads)
                    .Validate(x => x >= 1 && x <= 32 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Must be between 1 and 32[/]")));
            _options.MaxConcurrentDownloads = concurrency;
        }
        else if (choice.Contains("Stealth"))
        {
            _options.StealthMode = !_options.StealthMode;
            if (_options.StealthMode)
            {
                _options.RandomizeDelays = true;
                AnsiConsole.MarkupLine("[green]✓ Stealth mode enabled[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]✗ Stealth mode disabled[/]");
            }
            System.Threading.Thread.Sleep(1500);
        }
        else if (choice.Contains("Delay"))
        {
            var delay = AnsiConsole.Prompt(
                new TextPrompt<int>($"[cyan]Enter delay in milliseconds (100-30000):[/] ")
                    .DefaultValue(_options.RequestDelayMs)
                    .Validate(x => x >= 100 && x <= 30000 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Must be between 100 and 30000[/]")));
            _options.RequestDelayMs = delay;
            _options.StealthMode = true;
            _options.RandomizeDelays = false;
        }
        else if (choice.Contains("Max Pages"))
        {
            var pages = AnsiConsole.Prompt(
                new TextPrompt<int>($"[cyan]Enter max pages (10-10000):[/] ")
                    .DefaultValue(_options.MaxPages)
                    .Validate(x => x >= 10 && x <= 10000 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Must be between 10 and 10000[/]")));
            _options.MaxPages = pages;
        }
    }

    private void ConfigureFilteringOptions()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.MarkupLine("[bold yellow]FILTERING OPTIONS[/]");
        AnsiConsole.WriteLine();

        var options = new List<string>
        {
            $"[cyan]Min Width:[/] {_options.MinImageWidth}px",
            $"[cyan]Max Width:[/] {(_options.MaxImageWidth > 0 ? _options.MaxImageWidth : "Unlimited")}px",
            $"[cyan]Min Height:[/] {_options.MinImageHeight}px",
            $"[cyan]Max Height:[/] {(_options.MaxImageHeight > 0 ? _options.MaxImageHeight : "Unlimited")}px",
            $"[cyan]Min File Size:[/] {_options.MinFileSizeKb}KB",
            $"[cyan]Max File Size:[/] {(_options.MaxFileSizeKb > 0 ? _options.MaxFileSizeKb : "Unlimited")}KB",
            $"[cyan]Skip Thumbnails:[/] {(_options.SkipThumbnails ? "✓ Yes" : "✗ No")}",
            $"[cyan]Include GIFs:[/] {(_options.IncludeAnimatedGifs ? "✓ Yes" : "✗ No")}",
            "Back to Menu"
        };

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select filter to adjust:")
                .AddChoices(options));

        if (choice.Contains("Min Width"))
        {
            var width = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Min width in pixels (0 = no limit): ")
                    .DefaultValue(_options.MinImageWidth));
            _options.MinImageWidth = width;
        }
        else if (choice.Contains("Max Width"))
        {
            var width = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Max width in pixels (0 = no limit): ")
                    .DefaultValue(_options.MaxImageWidth));
            _options.MaxImageWidth = width;
        }
        else if (choice.Contains("Min Height"))
        {
            var height = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Min height in pixels (0 = no limit): ")
                    .DefaultValue(_options.MinImageHeight));
            _options.MinImageHeight = height;
        }
        else if (choice.Contains("Max Height"))
        {
            var height = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Max height in pixels (0 = no limit): ")
                    .DefaultValue(_options.MaxImageHeight));
            _options.MaxImageHeight = height;
        }
        else if (choice.Contains("Min File"))
        {
            var size = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Min file size in KB (0 = no limit): ")
                    .DefaultValue(_options.MinFileSizeKb));
            _options.MinFileSizeKb = size;
        }
        else if (choice.Contains("Max File"))
        {
            var size = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Max file size in KB (0 = no limit): ")
                    .DefaultValue(_options.MaxFileSizeKb));
            _options.MaxFileSizeKb = size;
        }
        else if (choice.Contains("Skip Thumbnails"))
        {
            _options.SkipThumbnails = !_options.SkipThumbnails;
            AnsiConsole.MarkupLine($"[{(_options.SkipThumbnails ? "green" : "yellow")}]Skip thumbnails: {(_options.SkipThumbnails ? "Enabled" : "Disabled")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
        else if (choice.Contains("Include GIFs"))
        {
            _options.IncludeAnimatedGifs = !_options.IncludeAnimatedGifs;
            AnsiConsole.MarkupLine($"[{(_options.IncludeAnimatedGifs ? "green" : "yellow")}]Include GIFs: {(_options.IncludeAnimatedGifs ? "Enabled" : "Disabled")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
    }

    private void ConfigureOutputSettings()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.MarkupLine("[bold yellow]OUTPUT SETTINGS[/]");
        AnsiConsole.WriteLine();

        var options = new List<string>
        {
            $"[cyan]Output Folder:[/] {(_options.OutputFolder ?? "Auto-generated")}",
            $"[cyan]Organization:[/] {_options.OutputOrganization}",
            $"[cyan]Convert To:[/] {(_options.ConvertToFormat ?? "No conversion")}",
            _options.ConvertToFormat == "jpg" ? $"[cyan]JPEG Quality:[/] {_options.JpegQuality}%" : "",
            "Back to Menu"
        };

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select option:")
                .AddChoices(options.Where(x => !string.IsNullOrEmpty(x)).ToList()));

        if (choice.Contains("Output Folder"))
        {
            var folder = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter output folder name (or press Enter for auto): ")
                    .AllowEmpty());
            _options.OutputFolder = string.IsNullOrEmpty(folder) ? null : folder;
        }
        else if (choice.Contains("Organization"))
        {
            var org = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select organization mode:[/]")
                    .AddChoices("flat", "by-page", "by-date", "mirrored"));
            _options.OutputOrganization = org;
        }
        else if (choice.Contains("Convert"))
        {
            var convert = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Convert images to:[/]")
                    .AddChoices("No conversion", "jpg", "png", "gif"));
            _options.ConvertToFormat = convert == "No conversion" ? null : convert;
        }
        else if (choice.Contains("JPEG Quality"))
        {
            var quality = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]JPEG quality (1-100): ")
                    .DefaultValue(_options.JpegQuality)
                    .Validate(x => x >= 1 && x <= 100 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Must be between 1 and 100[/]")));
            _options.JpegQuality = quality;
        }
    }

    private void ConfigureAdvancedFeatures()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.MarkupLine("[bold yellow]ADVANCED FEATURES[/]");
        AnsiConsole.WriteLine();

        var options = new List<string>
        {
            $"[cyan]JavaScript Rendering:[/] {(_options.EnableJavaScriptRendering ? "✓ Enabled" : "✗ Disabled")}",
            _options.EnableJavaScriptRendering ? $"[cyan]JS Wait Time:[/] {_options.JavaScriptWaitTimeMs}ms" : "",
            $"[cyan]Respect Robots.txt:[/] {(_options.RespectRobotsTxt ? "✓ Yes" : "✗ No")}",
            $"[cyan]Max Depth:[/] {(_options.MaxDepth > 0 ? _options.MaxDepth : "Unlimited")}",
            $"[cyan]File Logging:[/] {(_options.EnableFileLogging ? "✓ Enabled" : "✗ Disabled")}",
            $"[cyan]Checkpoints:[/] {(_options.EnableCheckpoints ? "✓ Enabled" : "✗ Disabled")}",
            "Back to Menu"
        };

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select feature:")
                .AddChoices(options.Where(x => !string.IsNullOrEmpty(x)).ToList()));

        if (choice.Contains("JavaScript"))
        {
            _options.EnableJavaScriptRendering = !_options.EnableJavaScriptRendering;
            AnsiConsole.MarkupLine($"[{(_options.EnableJavaScriptRendering ? "green" : "yellow")}]JS Rendering: {(_options.EnableJavaScriptRendering ? "Enabled" : "Disabled")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
        else if (choice.Contains("JS Wait"))
        {
            var wait = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Wait time in milliseconds (500-30000): ")
                    .DefaultValue(_options.JavaScriptWaitTimeMs)
                    .Validate(x => x >= 500 && x <= 30000 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Must be between 500 and 30000[/]")));
            _options.JavaScriptWaitTimeMs = wait;
        }
        else if (choice.Contains("Robots"))
        {
            _options.RespectRobotsTxt = !_options.RespectRobotsTxt;
            AnsiConsole.MarkupLine($"[{(_options.RespectRobotsTxt ? "green" : "yellow")}]Robots.txt: {(_options.RespectRobotsTxt ? "Respected" : "Ignored")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
        else if (choice.Contains("Max Depth"))
        {
            var depth = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]Max depth (0 = unlimited): ")
                    .DefaultValue(_options.MaxDepth));
            _options.MaxDepth = depth;
        }
        else if (choice.Contains("File Logging"))
        {
            _options.EnableFileLogging = !_options.EnableFileLogging;
            AnsiConsole.MarkupLine($"[{(_options.EnableFileLogging ? "green" : "yellow")}]File Logging: {(_options.EnableFileLogging ? "Enabled" : "Disabled")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
        else if (choice.Contains("Checkpoints"))
        {
            _options.EnableCheckpoints = !_options.EnableCheckpoints;
            AnsiConsole.MarkupLine($"[{(_options.EnableCheckpoints ? "green" : "yellow")}]Checkpoints: {(_options.EnableCheckpoints ? "Enabled" : "Disabled")}[/]");
            System.Threading.Thread.Sleep(1500);
        }
    }

    private void ViewCurrentSettings()
    {
        AnsiConsole.Clear();
        ShowBanner();
        AnsiConsole.MarkupLine("[bold cyan]CURRENT SETTINGS[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold cyan]Setting[/]")
            .AddColumn("[bold cyan]Value[/]");

        // Core settings
        table.AddRow("[cyan]URL[/]", _options.Url ?? "");
        table.AddRow("[cyan]Mode[/]", _options.IsDomainMode ? "Domain" : "Single Page");

        // Download settings
        table.AddRow("[green]Concurrent Downloads[/]", _options.MaxConcurrentDownloads.ToString());
        table.AddRow("[green]Stealth Mode[/]", _options.StealthMode ? "✓ Enabled" : "✗ Disabled");
        table.AddRow("[green]Request Delay[/]", $"{_options.RequestDelayMs}ms");
        table.AddRow("[green]Max Pages[/]", _options.MaxPages.ToString());

        // Filtering
        table.AddRow("[yellow]Min Width[/]", $"{_options.MinImageWidth}px");
        table.AddRow("[yellow]Max Width[/]", _options.MaxImageWidth > 0 ? $"{_options.MaxImageWidth}px" : "Unlimited");
        table.AddRow("[yellow]Min Height[/]", $"{_options.MinImageHeight}px");
        table.AddRow("[yellow]Max Height[/]", _options.MaxImageHeight > 0 ? $"{_options.MaxImageHeight}px" : "Unlimited");
        table.AddRow("[yellow]Min File Size[/]", $"{_options.MinFileSizeKb}KB");
        table.AddRow("[yellow]Max File Size[/]", _options.MaxFileSizeKb > 0 ? $"{_options.MaxFileSizeKb}KB" : "Unlimited");
        table.AddRow("[yellow]Skip Thumbnails[/]", _options.SkipThumbnails ? "✓ Yes" : "✗ No");
        table.AddRow("[yellow]Include GIFs[/]", _options.IncludeAnimatedGifs ? "✓ Yes" : "✗ No");

        // Output
        table.AddRow("[magenta]Output Folder[/]", _options.OutputFolder ?? "Auto-generated");
        table.AddRow("[magenta]Organization[/]", _options.OutputOrganization);
        table.AddRow("[magenta]Convert To[/]", _options.ConvertToFormat ?? "No conversion");
        if (_options.ConvertToFormat == "jpg")
            table.AddRow("[magenta]JPEG Quality[/]", $"{_options.JpegQuality}%");

        // Advanced
        table.AddRow("[blue]JavaScript Rendering[/]", _options.EnableJavaScriptRendering ? "✓ Enabled" : "✗ Disabled");
        table.AddRow("[blue]JS Wait Time[/]", $"{_options.JavaScriptWaitTimeMs}ms");
        table.AddRow("[blue]Respect Robots.txt[/]", _options.RespectRobotsTxt ? "✓ Yes" : "✗ No");
        table.AddRow("[blue]Max Depth[/]", _options.MaxDepth > 0 ? _options.MaxDepth.ToString() : "Unlimited");
        table.AddRow("[blue]File Logging[/]", _options.EnableFileLogging ? "✓ Enabled" : "✗ Disabled");
        table.AddRow("[blue]Checkpoints[/]", _options.EnableCheckpoints ? "✓ Enabled" : "✗ Disabled");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press Enter to return to menu...[/]");
        Console.ReadLine();
    }

    private void ShowBanner()
    {
        AnsiConsole.MarkupLine("[bold cyan]╔════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[bold cyan]║[/]  [bold]PixThief - Advanced Image Scraper[/]      [bold cyan]║[/]");
        AnsiConsole.MarkupLine("[bold cyan]╚════════════════════════════════════════╝[/]");
    }
}
