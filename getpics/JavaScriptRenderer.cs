using Microsoft.Playwright;

namespace PixThief;

/// <summary>
/// Handles JavaScript rendering using Playwright for modern sites with lazy-loaded content
/// This is the killer feature that enables support for JS-rendered images
/// </summary>
class JavaScriptRenderer : IDisposable
{
    private IBrowser? _browser;
    private readonly int _waitTimeMs;
    private bool _initialized;
    private bool _disposed;

    public JavaScriptRenderer(int waitTimeMs = 3000)
    {
        _waitTimeMs = waitTimeMs;
    }

    /// <summary>
    /// Initialize Playwright (one-time setup)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            var playwright = await Playwright.CreateAsync();
            _browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
            _initialized = true;
            Console.WriteLine("[INFO] JavaScript renderer initialized");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to initialize Playwright: {ex.Message}");
            Console.WriteLine("[INFO] Falling back to static HTML parsing (no JS rendering)");
        }
    }

    /// <summary>
    /// Render a page with JavaScript and return the HTML content
    /// </summary>
    public async Task<string?> RenderPageAsync(string url)
    {
        if (_browser == null || !_initialized)
            return null;

        try
        {
            await using var context = await _browser.NewContextAsync();
            var page = await context.NewPageAsync();

            try
            {
                // Navigate to the page
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                // Wait for dynamic content to load
                await page.WaitForTimeoutAsync(_waitTimeMs);

                // Scroll to bottom to trigger lazy loading
                await ScrollToBottomAsync(page);

                // Get the rendered HTML
                var content = await page.ContentAsync();

                return content;
            }
            finally
            {
                await page.CloseAsync();
                await context.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] JS rendering failed for {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Scroll to bottom of page to trigger lazy loading
    /// </summary>
    private async Task ScrollToBottomAsync(IPage page)
    {
        try
        {
            var lastHeight = await page.EvaluateAsync<long>("document.body.scrollHeight");

            while (true)
            {
                // Scroll down
                await page.EvaluateAsync("window.scrollBy(0, window.innerHeight)");

                // Wait for new content
                await page.WaitForTimeoutAsync(500);

                // Calculate new height
                var newHeight = await page.EvaluateAsync<long>("document.body.scrollHeight");

                if (newHeight == lastHeight)
                    break;

                lastHeight = newHeight;
            }

            // Scroll back to top
            await page.EvaluateAsync("window.scrollTo(0, 0)");
        }
        catch
        {
            // Scrolling failed, continue anyway
        }
    }

    /// <summary>
    /// Check if renderer is available
    /// </summary>
    public bool IsAvailable => _initialized && _browser != null;

    /// <summary>
    /// Cleanup
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            if (_browser != null)
                await _browser.CloseAsync();
        }
        catch { }

        _browser = null;
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
        catch { }

        _disposed = true;
    }
}
