using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Generic;

namespace PixThief;

/// <summary>
/// Parses robots.txt and sitemap.xml for efficient crawling
/// </summary>
class SitemapAndRobotsParser
{
    private readonly HttpClient _httpClient;
    private readonly string _domain;
    private readonly bool _respectRobotsTxt;
    private List<string>? _robotsDisallowPaths;

    public SitemapAndRobotsParser(HttpClient httpClient, string baseUrl, bool respectRobotsTxt)
    {
        _httpClient = httpClient;
        _domain = new Uri(baseUrl).Host;
        _respectRobotsTxt = respectRobotsTxt;
    }

    /// <summary>
    /// Check if a URL path is allowed by robots.txt
    /// </summary>
    public async Task<bool> IsAllowedByRobotsAsync(string url)
    {
        if (!_respectRobotsTxt)
            return true;

        if (_robotsDisallowPaths == null)
        {
            await LoadRobotsAsync();
        }

        var uri = new Uri(url);
        var path = uri.AbsolutePath;

        // Check against disallowed paths
        foreach (var disallowedPath in _robotsDisallowPaths ?? new List<string>())
        {
            if (path.StartsWith(disallowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extract all URLs from sitemap.xml for efficient page discovery
    /// </summary>
    public async Task<List<string>> ExtractSitemapUrlsAsync(string baseUrl)
    {
        var urls = new List<string>();

        try
        {
            var sitemapUrl = $"{new Uri(baseUrl).Scheme}://{new Uri(baseUrl).Host}/sitemap.xml";
            var response = await _httpClient.GetAsync(sitemapUrl);

            if (!response.IsSuccessStatusCode)
                return urls;

            var content = await response.Content.ReadAsStringAsync();
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(content);

            // Standard sitemap namespace
            var nsMgr = new XmlNamespaceManager(new NameTable());
            nsMgr.AddNamespace("s", "http://www.sitemaps.org/schemas/sitemap/0.9");

            // Get all loc elements
            var locNodes = xmlDoc.SelectNodes("//s:loc", nsMgr);
            if (locNodes.Count == 0)
            {
                // Try without namespace
                locNodes = xmlDoc.SelectNodes("//loc");
            }

            foreach (XmlNode node in locNodes)
            {
                var url = node.InnerText?.Trim();
                if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    if (uri.Host == _domain)
                    {
                        urls.Add(url);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Sitemap parsing failed, return empty list
            Console.WriteLine($"[INFO] Could not parse sitemap: {ex.Message}");
        }

        return urls;
    }

    /// <summary>
    /// Load and parse robots.txt
    /// </summary>
    private async Task LoadRobotsAsync()
    {
        _robotsDisallowPaths = new List<string>();

        try
        {
            var robotsUrl = $"https://{_domain}/robots.txt";
            var response = await _httpClient.GetAsync(robotsUrl);

            if (!response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync();
            ParseRobotsTxt(content);
        }
        catch
        {
            // Failed to load robots.txt, treat all paths as allowed
        }
    }

    /// <summary>
    /// Parse robots.txt content
    /// </summary>
    private void ParseRobotsTxt(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        bool matchesUserAgent = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip comments
            if (trimmedLine.StartsWith("#"))
                continue;

            // Check for user-agent
            if (trimmedLine.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                var userAgent = trimmedLine.Substring("User-agent:".Length).Trim();
                matchesUserAgent = userAgent == "*" || userAgent.Contains("*");
            }

            // Parse disallow rules for matching user-agent
            if (matchesUserAgent && trimmedLine.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                var disallowPath = trimmedLine.Substring("Disallow:".Length).Trim();
                if (!string.IsNullOrEmpty(disallowPath))
                {
                    _robotsDisallowPaths?.Add(disallowPath);
                }
            }
        }
    }
}
