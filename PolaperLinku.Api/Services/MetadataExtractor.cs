using HtmlAgilityPack;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace PolaperLinku.Api.Services;

public class MetadataExtractor
{
    private readonly HttpClient _httpClient;
    private readonly MetadataCache _cache;
    private readonly ILogger<MetadataExtractor>? _logger;

    public MetadataExtractor(HttpClient httpClient, MetadataCache cache, ILogger<MetadataExtractor>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    }

    public async Task<(string title, string? description, string? imageUrl)> ExtractMetadataAsync(string url)
    {
        if (_cache.TryGet(url, out var cachedMetadata))
        {
            return cachedMetadata;
        }

        try
        {
            (string title, string? description, string? imageUrl) metadata;

            if (IsXUrl(url))
            {
                // Para Twitter/X, usar fxtwitter como proxy para obtener metadatos
                metadata = await ExtractFromFxTwitterAsync(url);
            }
            else
            {
                // Primero intentar con HttpClient
                metadata = await ExtractFromHttpClientAsync(url);
                
                // Si no obtuvimos imagen, intentar con Playwright como fallback
                if (string.IsNullOrEmpty(metadata.imageUrl))
                {
                    try
                    {
                        var playwrightMetadata = await ExtractFromPlaywrightAsync(url);
                        if (!string.IsNullOrEmpty(playwrightMetadata.imageUrl))
                        {
                            metadata = playwrightMetadata;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Playwright fallback failed for {Url}", url);
                    }
                }
            }

            _cache.Set(url, metadata);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting metadata for {Url}", url);
            var fallback = GetFallbackMetadata(url);
            _cache.Set(url, fallback);
            return fallback;
        }
    }

    private async Task<(string title, string? description, string? imageUrl)> ExtractFromFxTwitterAsync(string url)
    {
        try
        {
            // Convertir URL de Twitter/X a fxtwitter para obtener metadatos
            var fxUrl = url
                .Replace("twitter.com", "fxtwitter.com")
                .Replace("x.com", "fxtwitter.com");

            var response = await _httpClient.GetAsync(fxUrl);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                var metadata = ExtractFromHtml(html, url);
                
                if (!string.IsNullOrEmpty(metadata.imageUrl))
                {
                    return metadata;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "fxtwitter extraction failed for {Url}", url);
        }

        // Fallback a Playwright si fxtwitter falla
        return await ExtractFromPlaywrightAsync(url);
    }

    private async Task<(string title, string? description, string? imageUrl)> ExtractFromHttpClientAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("HTTP request failed with status {StatusCode} for {Url}", response.StatusCode, url);
                return GetFallbackMetadata(url);
            }

            var html = await response.Content.ReadAsStringAsync();
            return ExtractFromHtml(html, url);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "HttpClient extraction failed for {Url}", url);
            return GetFallbackMetadata(url);
        }
    }

    private async Task<(string title, string? description, string? imageUrl)> ExtractFromPlaywrightAsync(string url)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
        });
        
        try
        {
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
            
            var page = await context.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 20000
            });

            // Esperar un poco más para sitios SPA
            await Task.Delay(1500);

            var html = await page.ContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = GetMetaContent(doc, "og:title")
                ?? GetMetaContent(doc, "twitter:title")
                ?? GetTitleFromHtml(doc)
                ?? ExtractXTitleFromPlaywright(doc, url)
                ?? GetDomainFromUrl(url);

            var description = GetMetaContent(doc, "og:description")
                ?? GetMetaContent(doc, "twitter:description")
                ?? GetMetaContent(doc, "description")
                ?? ExtractXDescriptionFromPlaywright(doc, url);

            var imageUrl = GetMetaContent(doc, "og:image")
                ?? GetMetaContent(doc, "twitter:image")
                ?? GetMetaContent(doc, "twitter:image:src");

            // Hacer URL absoluta si es relativa
            if (!string.IsNullOrEmpty(imageUrl))
            {
                imageUrl = MakeAbsoluteUrl(imageUrl, url);
            }

            return (CleanText(title), description != null ? CleanText(description) : null, imageUrl);
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private string? ExtractXTitleFromPlaywright(HtmlDocument doc, string url)
    {
        if (!IsXUrl(url)) return null;
        
        var handle = ExtractUserHandleFromUrl(url);
        if (!string.IsNullOrEmpty(handle))
        {
            var tweetText = ExtractTweetText(doc);
            if (!string.IsNullOrEmpty(tweetText))
            {
                return $"@{handle}: {TruncateText(tweetText, 100)}";
            }
        }

        return GetTitleFromHtml(doc);
    }

    private string? ExtractXDescriptionFromPlaywright(HtmlDocument doc, string url)
    {
        var tweetText = ExtractTweetText(doc);
        if (!string.IsNullOrEmpty(tweetText))
        {
            return TruncateText(tweetText, 200);
        }

        var userBio = ExtractUserDescription(doc);
        if (!string.IsNullOrEmpty(userBio))
        {
            return userBio;
        }

        return ExtractFirstParagraph(doc);
    }

    private string? ExtractTweetText(HtmlDocument doc)
    {
        var tweetNodes = doc.DocumentNode.SelectNodes("//div[@data-testid='tweetText']");
        if (tweetNodes != null && tweetNodes.Count > 0)
        {
            return tweetNodes[0].InnerText?.Trim();
        }
        return null;
    }

    private (string title, string? description, string? imageUrl) ExtractFromHtml(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractTitle(doc, url);
        var description = ExtractDescription(doc, url);
        var imageUrl = ExtractImage(doc, url);

        return (title, description, imageUrl);
    }

    private string ExtractTitle(HtmlDocument doc, string url)
    {
        var title = GetMetaContent(doc, "og:title")
            ?? GetMetaContent(doc, "twitter:title")
            ?? GetMetaContent(doc, "title")
            ?? GetTitleFromHtml(doc)
            ?? GetDomainFromUrl(url);
        
        return CleanText(title);
    }

    private string? ExtractDescription(HtmlDocument doc, string url)
    {
        var description = GetMetaContent(doc, "og:description")
            ?? GetMetaContent(doc, "twitter:description")
            ?? GetMetaContent(doc, "description")
            ?? ExtractFirstParagraph(doc);
        
        return description != null ? CleanText(description) : null;
    }

    private string? ExtractImage(HtmlDocument doc, string url)
    {
        var imageUrl = GetMetaContent(doc, "og:image")
            ?? GetMetaContent(doc, "twitter:image")
            ?? GetMetaContent(doc, "twitter:image:src")
            ?? GetFirstImage(doc);
        
        return imageUrl != null ? MakeAbsoluteUrl(imageUrl, url) : null;
    }

    private string? GetFirstImage(HtmlDocument doc)
    {
        // Buscar una imagen principal (hero, banner, etc.)
        var imgNode = doc.DocumentNode.SelectSingleNode("//img[@class[contains(., 'hero')]]")
            ?? doc.DocumentNode.SelectSingleNode("//img[@class[contains(., 'banner')]]")
            ?? doc.DocumentNode.SelectSingleNode("//img[@class[contains(., 'logo')]]")
            ?? doc.DocumentNode.SelectSingleNode("//main//img")
            ?? doc.DocumentNode.SelectSingleNode("//article//img");
        
        var src = imgNode?.GetAttributeValue("src", null);
        
        // Solo devolver si es una URL válida (no data URI o placeholder)
        if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:") && src.Length > 10)
        {
            return src;
        }
        
        return null;
    }

    private string? ExtractUserDescription(HtmlDocument doc)
    {
        var bioNode = doc.DocumentNode.SelectSingleNode("//div[@data-testid='UserDescription']");
        return bioNode?.InnerText?.Trim();
    }

    private string? ExtractFirstParagraph(HtmlDocument doc)
    {
        var pNodes = doc.DocumentNode.SelectNodes("//p");
        if (pNodes != null)
        {
            foreach (var p in pNodes)
            {
                var text = p.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 20)
                {
                    return TruncateText(text, 200);
                }
            }
        }
        return null;
    }

    private string? GetMetaContent(HtmlDocument doc, string name)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{name}']")
            ?? doc.DocumentNode.SelectSingleNode($"//meta[@name='{name}']")
            ?? doc.DocumentNode.SelectSingleNode($"//meta[@itemprop='{name}']");
        return node?.GetAttributeValue("content", null);
    }

    private string? GetTitleFromHtml(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText?.Trim();
        return !string.IsNullOrEmpty(title) ? TruncateText(title, 100) : null;
    }

    private string GetDomainFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Replace("www.", "");
        }
        catch
        {
            return url;
        }
    }

    private string ExtractUserHandleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return pathParts.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool IsXUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Contains("twitter.com") || uri.Host.Contains("x.com");
        }
        catch
        {
            return false;
        }
    }

    private string? MakeAbsoluteUrl(string url, string baseUrl)
    {
        if (string.IsNullOrEmpty(url)) return null;
        
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        try
        {
            return new Uri(new Uri(baseUrl), url).AbsoluteUri;
        }
        catch
        {
            return url;
        }
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
        cleaned = WebUtility.HtmlDecode(cleaned);
        
        return cleaned;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text[..maxLength].Trim() + "...";
    }

    private (string title, string? description, string? imageUrl) GetFallbackMetadata(string url)
    {
        var domain = GetDomainFromUrl(url);
        var title = domain;
        var description = $"Link from {domain}";
        
        if (IsXUrl(url))
        {
            var handle = ExtractUserHandleFromUrl(url);
            if (!string.IsNullOrEmpty(handle))
            {
                title = $"@{handle} - X (Twitter)";
            }
        }

        return (title, description, null);
    }
}
