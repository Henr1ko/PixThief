using AngleSharp;
using AngleSharp.Dom;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Gif;

namespace PixThief;

/// <summary>
/// Enhanced ImageScraper with parallel downloads, filtering, hash-based deduplication,
/// robots.txt respect, sitemap parsing, and JavaScript rendering support
/// </summary>
class ImageScraperEnhanced
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".svg", ".bmp", ".ico" };
    private static readonly string[] AllImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", ".ico" };

    private readonly ConcurrentHashSet<string> _visitedUrls = new();
    private readonly ConcurrentHashSet<string> _downloadedImages = new();
    private readonly ConcurrentHashSet<string> _downloadedHashes = new();
    private readonly ConcurrentDictionary<string, string> _urlToHash = new();

    private readonly ScraperOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IBrowsingContext _browsingContext;
    private readonly Random _random;
    private readonly ProgressStats _stats;

    private readonly FileLogger? _logger;
    private readonly CheckpointManager _checkpointManager;
    private readonly SitemapAndRobotsParser? _robotsParser;
    private readonly JavaScriptRenderer? _jsRenderer;

    private string? _outputFolder;
    private SemaphoreSlim _downloadSemaphore;

    public ImageScraperEnhanced(ScraperOptions options, ProgressStats stats)
    {
        _options = options;
        _stats = stats;
        _downloadSemaphore = new SemaphoreSlim(options.MaxConcurrentDownloads, options.MaxConcurrentDownloads);

        // Initialize HTTP client
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Setup headers
        _random = new Random();
        if (options.StealthMode)
        {
            AddStealthHeaders();
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        _browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);

        // Initialize logging
        _logger = options.EnableFileLogging ? new FileLogger(options.LogFilePath) : null;
        Log("INFO", "ImageScraper initialized");

        // Initialize checkpoint manager
        _checkpointManager = new CheckpointManager(options.CheckpointFile);

        // Initialize robots.txt parser
        if (options.RespectRobotsTxt)
        {
            _robotsParser = new SitemapAndRobotsParser(_httpClient, options.Url ?? "", true);
        }

        // Initialize JS renderer if enabled
        if (options.EnableJavaScriptRendering)
        {
            _jsRenderer = new JavaScriptRenderer(options.JavaScriptWaitTimeMs);
        }


    }

    /// <summary>
    /// Download images for single page with all enhancements
    /// </summary>
    public async Task DownloadImagesForSinglePageAsync()
    {
        if (string.IsNullOrEmpty(_options.Url))
            throw new ArgumentException("URL is required");


        Log("INFO", "Starting single-page image download...");

        try
        {
            // Initialize JS renderer if needed
            if (_jsRenderer != null)
            {
                await _jsRenderer.InitializeAsync();
            }

            _outputFolder = DetermineOutputFolder(_options.Url);
            Directory.CreateDirectory(_outputFolder);

            await ExtractAndDownloadImagesFromPageAsync(_options.Url, 0);


            Log("INFO", $"Download complete. Total images found: {_stats.ImagesFound}");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Fatal error: {ex.Message}");
            throw;
        }
        finally
        {
            _jsRenderer?.Dispose();
            _logger?.Dispose();
        }
    }

    /// <summary>
    /// Download images for entire domain with all enhancements
    /// </summary>
    public async Task DownloadImagesForDomainAsync()
    {
        if (string.IsNullOrEmpty(_options.Url))
            throw new ArgumentException("URL is required");


        Log("INFO", "Starting domain-wide crawl with enhanced features...");

        try
        {
            // Initialize JS renderer if needed
            if (_jsRenderer != null)
            {
                await _jsRenderer.InitializeAsync();
            }

            // Load checkpoint if exists
            var checkpoint = _checkpointManager.LoadCheckpoint();
            if (checkpoint != null && checkpoint.Url == _options.Url)
            {
                Log("INFO", $"Resuming from checkpoint created at {checkpoint.CreatedAt}");
                _visitedUrls.UnionWith(checkpoint.VisitedUrls);
                _downloadedImages.UnionWith(checkpoint.DownloadedImages);
                _downloadedHashes.UnionWith(checkpoint.DownloadedHashes);
                _stats.PagesCrawled = checkpoint.PagesProcessed;
                _stats.ImagesFound = checkpoint.ImagesFound;
                _stats.ImagesDownloaded = checkpoint.ImagesDownloaded;
                _stats.ImagesFailed = checkpoint.ImagesFailed;
                _stats.BytesDownloaded = checkpoint.BytesDownloaded;
            }
            else
            {
                _checkpointManager.CreateCheckpoint(_options.Url, SerializeOptions());
            }

            _outputFolder = DetermineOutputFolder(_options.Url);
            Directory.CreateDirectory(_outputFolder);

            var uri = new Uri(_options.Url);
            var queue = new Queue<(string url, int depth)>();
            queue.Enqueue((_options.Url, 0));

            // Try to get URLs from sitemap first for efficiency
            var sitemapUrls = await _robotsParser?.ExtractSitemapUrlsAsync(_options.Url) ?? new List<string>();
            if (sitemapUrls.Count > 0)
            {
                Log("INFO", $"Found {sitemapUrls.Count} URLs in sitemap.xml");
                foreach (var url in sitemapUrls)
                {
                    if (!_visitedUrls.Contains(url))
                        queue.Enqueue((url, 0));
                }
            }

            int pageCount = _stats.PagesCrawled;

            while (queue.Count > 0 && pageCount < _options.MaxPages)
            {
                var (pageUrl, depth) = queue.Dequeue();

                // Check depth limit
                if (_options.MaxDepth > 0 && depth > _options.MaxDepth)
                    continue;

                if (_visitedUrls.Contains(pageUrl))
                    continue;

                // Check robots.txt
                if (!await (_robotsParser?.IsAllowedByRobotsAsync(pageUrl) ?? Task.FromResult(true)))
                {
                    Log("INFO", $"Skipping {pageUrl} (blocked by robots.txt)");
                    continue;
                }

                _visitedUrls.Add(pageUrl);
                pageCount++;
                _stats.PagesCrawled = pageCount;

                Log("INFO", $"Crawling [{pageCount}/{_options.MaxPages}]: {pageUrl} (depth: {depth})");
                _stats.AddAction($"Crawling: {new Uri(pageUrl).Host}{new Uri(pageUrl).AbsolutePath}");

                try
                {
                    if (_options.StealthMode && pageCount > 1)
                    {
                        var delay = GetRandomizedDelay(_options.RequestDelayMs);
                        await Task.Delay(delay);
                    }

                    await ExtractAndDownloadImagesFromPageAsync(pageUrl, depth);

                    // Extract links for next pages
                    var links = await ExtractLinksFromPageAsync(pageUrl);
                    foreach (var link in links)
                    {
                        if (!_visitedUrls.Contains(link) && IsSameDomain(link, uri))
                        {
                            queue.Enqueue((link, depth + 1));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("WARN", $"Error processing page {pageUrl}: {ex.Message}");
                    _stats.AddAction($"Error: {new Uri(pageUrl).Host}");
                }

                // Update checkpoint periodically
                if (pageCount % 10 == 0)
                {
                    var visited = new HashSet<string>();
                    var downloaded = new HashSet<string>();
                    var hashes = new HashSet<string>();

                    foreach (var item in _visitedUrls) visited.Add(item);
                    foreach (var item in _downloadedImages) downloaded.Add(item);
                    foreach (var item in _downloadedHashes) hashes.Add(item);

                    _checkpointManager.UpdateCheckpoint(visited, downloaded, hashes,
                        _stats.PagesCrawled, _stats.ImagesFound, _stats.ImagesDownloaded,
                        _stats.ImagesFailed, _stats.BytesDownloaded);
                }
            }

            // Delete checkpoint on success
            _checkpointManager.DeleteCheckpoint();

            Log("INFO", $"Crawling complete. Processed {pageCount} pages, found {_stats.ImagesFound} images.");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Fatal error: {ex.Message}");

            throw;
        }
        finally
        {
            _jsRenderer?.Dispose();
            _logger?.Dispose();
        }
    }

    /// <summary>
    /// Extract and download images from a page with JS rendering support
    /// </summary>
    private async Task ExtractAndDownloadImagesFromPageAsync(string pageUrl, int depth)
    {
        try
        {
            var html = await FetchHtmlAsync(pageUrl);
            if (string.IsNullOrEmpty(html))
                return;

            // Try JS rendering if enabled and static parsing finds few images
            if (_jsRenderer?.IsAvailable == true && _options.EnableJavaScriptRendering)
            {
                var renderedHtml = await _jsRenderer.RenderPageAsync(pageUrl);
                if (!string.IsNullOrEmpty(renderedHtml))
                {
                    Log("DEBUG", "Using JavaScript-rendered content");
                    html = renderedHtml;
                }
            }

            var document = await _browsingContext.OpenAsync(req => req.Content(html));
            var imageUrls = new HashSet<string>();

            // Extract from all sources
            ExtractFromImageTags(document, imageUrls, pageUrl);
            ExtractFromPictureElements(document, imageUrls, pageUrl);
            ExtractFromBackgroundImages(document, imageUrls, pageUrl);
            ExtractFromDataAttributes(document, imageUrls, pageUrl);
            ExtractFromMediaElements(document, imageUrls, pageUrl);
            ExtractFromInlineStyles(html, imageUrls, pageUrl);
            ExtractFromTextContent(html, imageUrls, pageUrl);

            // Filter URLs
            var validImageUrls = imageUrls
                .Where(url => IsValidImageUrl(url) && ShouldDownloadImage(url))
                .Distinct()
                .ToList();

            _stats.ImagesFound += validImageUrls.Count;
            Log("INFO", $"Found {validImageUrls.Count} valid images on {pageUrl}");
            _stats.AddAction($"Found {validImageUrls.Count} images");

            // Download images in parallel with rate limiting
            var tasks = validImageUrls.Select(url => DownloadImageAsync(url, pageUrl)).ToList();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error extracting images from {pageUrl}", ex);
        }
    }

    /// <summary>
    /// Check if image URL should be downloaded based on filtering rules
    /// </summary>
    private bool ShouldDownloadImage(string url)
    {
        // URL regex filter
        if (!string.IsNullOrEmpty(_options.UrlRegexFilter))
        {
            try
            {
                if (!Regex.IsMatch(url, _options.UrlRegexFilter))
                    return false;
            }
            catch { }
        }

        return true;
    }

    /// <summary>
    /// Download image with parallel semaphore, hash deduplication, and filtering
    /// </summary>
    private async Task DownloadImageAsync(string imageUrl, string sourcePageUrl)
    {
        if (_downloadedImages.Contains(imageUrl))
            return;

        _downloadedImages.Add(imageUrl);

        // Use semaphore to limit concurrent downloads
        await _downloadSemaphore.WaitAsync();
        try
        {
            if (_options.StealthMode)
            {
                var delay = GetRandomizedDelay(_options.RequestDelayMs / 2);
                await Task.Delay(delay);
            }

            var fileName = ExtractFileNameFromUrl(imageUrl);
            var originalFileName = fileName;

            // Apply conversion if requested
            if (!string.IsNullOrEmpty(_options.ConvertToFormat))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var newExt = _options.ConvertToFormat == "jpg" ? ".jpg" : $".{_options.ConvertToFormat}";
                fileName = nameWithoutExt + newExt;
            }

            var filePath = GetOutputPath(fileName, sourcePageUrl);
            filePath = GetUniqueFilePath(filePath);

            Log("DEBUG", $"Downloading: {imageUrl}");
            _stats.CurrentlyDownloading.Add(imageUrl);
            _stats.AddAction($"Downloading: {Path.GetFileName(imageUrl)}");

            var imageData = await _httpClient.GetByteArrayAsync(imageUrl);

            // Calculate hash for deduplication
            var fileHash = PlatformUtilities.CalculateFileHash(imageData);
            if (_downloadedHashes.Contains(fileHash))
            {
                Log("INFO", $"Skipping duplicate (hash match): {imageUrl}");
                _stats.ImagesSkipped++;
                _stats.AddAction($"Skipped (duplicate): {Path.GetFileName(imageUrl)}");
                return;
            }

            _downloadedHashes.Add(fileHash);
            _urlToHash[imageUrl] = fileHash;

            // Check file size filter
            if (_options.MaxFileSizeKb > 0 && imageData.Length > PlatformUtilities.ConvertKbToBytes(_options.MaxFileSizeKb))
            {
                Log("INFO", $"Skipping (too large): {imageUrl}");
                _stats.ImagesSkipped++;
                _stats.AddAction($"Skipped (too large): {Path.GetFileName(imageUrl)}");
                return;
            }

            if (_options.MinFileSizeKb > 0 && imageData.Length < PlatformUtilities.ConvertKbToBytes(_options.MinFileSizeKb))
            {
                Log("INFO", $"Skipping (too small): {imageUrl}");
                _stats.ImagesSkipped++;
                _stats.AddAction($"Skipped (too small): {Path.GetFileName(imageUrl)}");
                return;
            }

            // Check image dimensions if needed
            if (_options.MinImageWidth > 0 || _options.MinImageHeight > 0 ||
                _options.MaxImageWidth > 0 || _options.MaxImageHeight > 0 || _options.SkipThumbnails)
            {
                try
                {
                    using var stream = new MemoryStream(imageData);
                    using var image = await Image.LoadAsync(stream);

                    // Skip thumbnails: detect small images (both dimensions < 200px)
                    if (_options.SkipThumbnails && image.Width < 200 && image.Height < 200)
                    {
                        Log("INFO", $"Skipping (thumbnail): {imageUrl} ({image.Width}x{image.Height}px)");
                        _stats.ImagesSkipped++;
                        _stats.AddAction($"Skipped (thumbnail): {Path.GetFileName(imageUrl)}");
                        return;
                    }

                    if (_options.MinImageWidth > 0 && image.Width < _options.MinImageWidth)
                    {
                        Log("INFO", $"Skipping (too narrow): {imageUrl} ({image.Width}px)");
                        _stats.ImagesSkipped++;
                        return;
                    }

                    if (_options.MinImageHeight > 0 && image.Height < _options.MinImageHeight)
                    {
                        Log("INFO", $"Skipping (too short): {imageUrl} ({image.Height}px)");
                        _stats.ImagesSkipped++;
                        return;
                    }

                    if (_options.MaxImageWidth > 0 && image.Width > _options.MaxImageWidth)
                    {
                        Log("INFO", $"Skipping (too wide): {imageUrl} ({image.Width}px)");
                        _stats.ImagesSkipped++;
                        return;
                    }

                    if (_options.MaxImageHeight > 0 && image.Height > _options.MaxImageHeight)
                    {
                        Log("INFO", $"Skipping (too tall): {imageUrl} ({image.Height}px)");
                        _stats.ImagesSkipped++;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log("WARN", $"Failed to check dimensions: {ex.Message}");
                }
            }

            // Save the image
            if (!string.IsNullOrEmpty(_options.ConvertToFormat))
            {
                var success = await ConvertAndSaveImageAsync(imageData, filePath, _options.ConvertToFormat);
                if (success)
                {
                    _stats.ImagesDownloaded++;
                    _stats.BytesDownloaded += imageData.Length;
                    Log("INFO", $"Downloaded & converted: {Path.GetFileName(filePath)}");
                    _stats.AddAction($"Downloaded: {Path.GetFileName(filePath)}");
                }
                else
                {
                    _stats.ImagesFailed++;
                    _stats.AddAction($"Failed: {Path.GetFileName(imageUrl)}");
                }
            }
            else
            {
                await File.WriteAllBytesAsync(filePath, imageData);
                _stats.ImagesDownloaded++;
                _stats.BytesDownloaded += imageData.Length;
                Log("INFO", $"Downloaded: {Path.GetFileName(filePath)}");
                _stats.AddAction($"Downloaded: {Path.GetFileName(filePath)}");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Failed to download {imageUrl}: {ex.Message}");
            _stats.ImagesFailed++;
            _stats.AddAction($"Failed: {new Uri(imageUrl).Host}");
        }
        finally
        {
            _stats.CurrentlyDownloading.Remove(imageUrl);
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Get output path based on organization option
    /// </summary>
    private string GetOutputPath(string fileName, string sourcePageUrl)
    {
        var baseFolder = _outputFolder ?? ".";

        return _options.OutputOrganization switch
        {
            "by-page" => Path.Combine(baseFolder, new Uri(sourcePageUrl).Host, fileName),
            "by-date" => Path.Combine(baseFolder, DateTime.Now.ToString("yyyy-MM-dd"), fileName),
            "mirrored" => Path.Combine(baseFolder, new Uri(sourcePageUrl).Host,
                new Uri(sourcePageUrl).AbsolutePath.Trim('/'), fileName),
            _ => Path.Combine(baseFolder, fileName) // flat
        };
    }

    // Keep all existing helper methods...
    private void AddStealthHeaders()
    {
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        };

        var userAgent = userAgents[_random.Next(userAgents.Length)];
        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    private int GetRandomizedDelay(int baseDelayMs)
    {
        if (_options.RandomizeDelays)
        {
            var variance = (int)(baseDelayMs * 0.4);
            var random1 = _random.NextDouble();
            var random2 = _random.NextDouble();
            var gaussian = Math.Sqrt(-2.0 * Math.Log(random1)) * Math.Cos(2.0 * Math.PI * random2);
            var scaledVariance = (int)(gaussian * variance / 2);
            scaledVariance = Math.Clamp(scaledVariance, -variance, variance);
            var finalDelay = baseDelayMs + scaledVariance;
            return Math.Max(200, finalDelay);
        }
        return baseDelayMs;
    }

    private void ExtractFromImageTags(IDocument document, HashSet<string> imageUrls, string pageUrl)
    {
        var imageElements = document.QuerySelectorAll("img");
        foreach (var img in imageElements)
        {
            var srcSet = img.GetAttribute("srcset");
            if (!string.IsNullOrEmpty(srcSet))
            {
                var urls = ParseSrcSet(srcSet);
                foreach (var url in urls)
                    imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }

            var src = img.GetAttribute("src");
            if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
                imageUrls.Add(ConvertToAbsoluteUrl(src, pageUrl));
        }
    }

    private void ExtractFromPictureElements(IDocument document, HashSet<string> imageUrls, string pageUrl)
    {
        var pictures = document.QuerySelectorAll("picture");
        foreach (var picture in pictures)
        {
            var sources = picture.QuerySelectorAll("source");
            foreach (var source in sources)
            {
                var srcSet = source.GetAttribute("srcset");
                if (!string.IsNullOrEmpty(srcSet))
                {
                    var urls = ParseSrcSet(srcSet);
                    foreach (var url in urls)
                        imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
                }
            }

            var img = picture.QuerySelector("img");
            if (img != null)
            {
                var src = img.GetAttribute("src");
                if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
                    imageUrls.Add(ConvertToAbsoluteUrl(src, pageUrl));
            }
        }
    }

    private void ExtractFromBackgroundImages(IDocument document, HashSet<string> imageUrls, string pageUrl)
    {
        var allElements = document.QuerySelectorAll("*");
        foreach (var element in allElements)
        {
            var style = element.GetAttribute("style");
            if (!string.IsNullOrEmpty(style))
            {
                var bgUrls = ExtractUrlsFromCss(style);
                foreach (var url in bgUrls)
                    imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }
        }
    }

    private void ExtractFromDataAttributes(IDocument document, HashSet<string> imageUrls, string pageUrl)
    {
        var elements = document.QuerySelectorAll("[data-src], [data-image], [data-background], [data-thumbnail], [data-thumb]");
        foreach (var element in elements)
        {
            foreach (var attr in new[] { "data-src", "data-image", "data-background", "data-thumbnail", "data-thumb", "data-lazy-src" })
            {
                var url = element.GetAttribute(attr);
                if (!string.IsNullOrEmpty(url) && IsLikelyImageUrl(url))
                    imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }
        }
    }

    private void ExtractFromMediaElements(IDocument document, HashSet<string> imageUrls, string pageUrl)
    {
        var videos = document.QuerySelectorAll("video");
        foreach (var video in videos)
        {
            var poster = video.GetAttribute("poster");
            if (!string.IsNullOrEmpty(poster))
                imageUrls.Add(ConvertToAbsoluteUrl(poster, pageUrl));
        }
    }

    private void ExtractFromInlineStyles(string html, HashSet<string> imageUrls, string pageUrl)
    {
        var stylePattern = @"(?:background-image|background)\s*:\s*url\(['""]?([^)'""])+['""]?\)";
        var matches = Regex.Matches(html, stylePattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var url = match.Groups[1].Value.Trim();
                if (IsLikelyImageUrl(url))
                    imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }
        }
    }

    private void ExtractFromTextContent(string html, HashSet<string> imageUrls, string pageUrl)
    {
        var urlPattern = @"""(?:image|url|src|thumbnail|thumb|poster|bg|background)["":\s]*""?\s*:\s*""([^""]+\.(?:jpg|jpeg|png|gif|webp|svg|bmp|ico))""";
        var matches = Regex.Matches(html, urlPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var url = match.Groups[1].Value.Trim();
                imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }
        }

        var absUrlPattern = @"""?((?:https?:)?//[^\s""'<>]+\.(?:jpg|jpeg|png|gif|webp|svg|bmp|ico))""?";
        var absMatches = Regex.Matches(html, absUrlPattern, RegexOptions.IgnoreCase);

        foreach (Match match in absMatches)
        {
            if (match.Groups.Count > 1)
            {
                var url = match.Groups[1].Value.Trim().TrimEnd('"', '\'', ',', ';', ':');
                if (IsLikelyImageUrl(url))
                    imageUrls.Add(ConvertToAbsoluteUrl(url, pageUrl));
            }
        }
    }

    private List<string> ExtractUrlsFromCss(string css)
    {
        var urls = new List<string>();
        var pattern = @"url\(['""]?([^)'""])+['""]?\)";
        var matches = Regex.Matches(css, pattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
                urls.Add(match.Groups[1].Value.Trim());
        }

        return urls;
    }

    private async Task<List<string>> ExtractLinksFromPageAsync(string pageUrl)
    {
        var links = new List<string>();

        try
        {
            var html = await FetchHtmlAsync(pageUrl);
            if (string.IsNullOrEmpty(html))
                return links;

            var document = await _browsingContext.OpenAsync(req => req.Content(html));
            var anchorElements = document.QuerySelectorAll("a");

            foreach (var anchor in anchorElements)
            {
                var href = anchor.GetAttribute("href");
                if (string.IsNullOrEmpty(href))
                    continue;

                var absoluteUrl = ConvertToAbsoluteUrl(href, pageUrl);

                if (Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var normalizedUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                    if (!string.IsNullOrEmpty(uri.Query))
                        normalizedUrl += uri.Query;

                    links.Add(normalizedUrl);
                }
            }
        }
        catch (Exception ex)
        {
            Log("WARN", $"Error extracting links from {pageUrl}: {ex.Message}");
        }

        return links;
    }

    private async Task<string?> FetchHtmlAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Log("WARN", $"Rate limited (429). Backing off...");
                await Task.Delay(_options.RequestDelayMs * 3);
                response = await _httpClient.GetAsync(url);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            Log("ERROR", $"Failed to fetch {url}: {ex.Message}");
            throw new Exception($"Failed to fetch {url}", ex);
        }
    }

    private bool IsSameDomain(string url, Uri originalUri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals(originalUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValidImageUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        try
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri && uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            var path = uri.IsAbsoluteUri ? uri.AbsolutePath : url;
            var extensions = _options.IncludeAnimatedGifs ? AllImageExtensions : ImageExtensions;

            return extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private bool IsLikelyImageUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        url = url.ToLower().Split('?')[0];
        var extensions = _options.IncludeAnimatedGifs ? AllImageExtensions : ImageExtensions;
        return extensions.Any(ext => url.EndsWith(ext));
    }

    private string ConvertToAbsoluteUrl(string relativeUrl, string pageUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl))
            return "";

        try
        {
            if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out _))
                return relativeUrl;

            var pageUri = new Uri(pageUrl);
            var absoluteUri = new Uri(pageUri, relativeUrl);
            return absoluteUri.ToString();
        }
        catch
        {
            return "";
        }
    }

    private string ExtractFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrEmpty(fileName) || fileName == "/")
                fileName = "image";

            if (!Path.HasExtension(fileName))
                fileName += ".jpg";

            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidRegex = new Regex($"[{invalidChars}]");
            fileName = invalidRegex.Replace(fileName, "_");

            return fileName;
        }
        catch
        {
            return "image.jpg";
        }
    }

    private string GetUniqueFilePath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(filePath))
            return filePath;

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        int counter = 1;
        while (true)
        {
            var newFileName = $"{fileName}_{counter}{extension}";
            var newFilePath = Path.Combine(directory ?? ".", newFileName);

            if (!File.Exists(newFilePath))
                return newFilePath;

            counter++;
        }
    }

    private string DetermineOutputFolder(string url)
    {
        if (!string.IsNullOrEmpty(_options.OutputFolder))
            return _options.OutputFolder;

        var uri = new Uri(url);
        var domain = uri.Host.Replace("www.", "");
        var pathSlug = uri.AbsolutePath.Trim('/').Replace("/", "_");

        if (!string.IsNullOrEmpty(pathSlug))
            return $"{domain}_{pathSlug}";
        else
            return $"{domain}_images";
    }

    private List<string> ParseSrcSet(string srcSet)
    {
        var urls = new List<string>();
        var entries = srcSet.Split(',');

        foreach (var entry in entries)
        {
            var parts = entry.Trim().Split(' ');
            if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
                urls.Add(parts[0]);
        }

        return urls;
    }

    private async Task<bool> ConvertAndSaveImageAsync(byte[] imageData, string targetFile, string targetFormat)
    {
        try
        {
            var directory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var inputStream = new MemoryStream(imageData);
            using var image = await Image.LoadAsync(inputStream);

            switch (targetFormat.ToLower())
            {
                case "jpg":
                case "jpeg":
                    var jpegEncoder = new JpegEncoder { Quality = _options.JpegQuality };
                    await image.SaveAsync(targetFile, jpegEncoder);
                    break;
                case "png":
                    var pngEncoder = new PngEncoder();
                    await image.SaveAsync(targetFile, pngEncoder);
                    break;
                case "gif":
                    var gifEncoder = new GifEncoder();
                    await image.SaveAsync(targetFile, gifEncoder);
                    break;
                default:
                    await File.WriteAllBytesAsync(targetFile, imageData);
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Conversion failed: {ex.Message}. Saving original.");
            try
            {
                var directory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllBytesAsync(targetFile, imageData);
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    private void Log(string category, string message)
    {
        _logger?.Log(category, message);
        if (_options.IsVerbose)
            Console.WriteLine($"[{category}] {message}");
    }

    private Dictionary<string, string> SerializeOptions()
    {
        var dict = new Dictionary<string, string>
        {
            { "Url", _options.Url ?? "" },
            { "MaxPages", _options.MaxPages.ToString() },
            { "MaxDepth", _options.MaxDepth.ToString() },
            { "MaxConcurrentDownloads", _options.MaxConcurrentDownloads.ToString() }
        };
        return dict;
    }
}

/// <summary>
/// Thread-safe HashSet wrapper
/// </summary>
public class ConcurrentHashSet<T> where T : notnull
{
    private readonly HashSet<T> _set = new();
    private readonly object _lock = new();

    public void Add(T item)
    {
        lock (_lock)
            _set.Add(item);
    }

    public bool Contains(T item)
    {
        lock (_lock)
            return _set.Contains(item);
    }

    public bool TryRemove(T item)
    {
        lock (_lock)
            return _set.Remove(item);
    }

    public void UnionWith(IEnumerable<T> other)
    {
        lock (_lock)
            _set.UnionWith(other);
    }

    public int Count
    {
        get
        {
            lock (_lock)
                return _set.Count;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
            return _set.ToList().GetEnumerator();
    }
}
