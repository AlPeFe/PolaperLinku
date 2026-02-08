using HtmlAgilityPack;
using Microsoft.Playwright;
using System.Net;
using System.Text.RegularExpressions;

namespace PolaperLinku.Api.Services;

public class MetadataExtractor
{
    private readonly HttpClient _httpClient;
    private readonly MetadataCache _cache;

    public MetadataExtractor(HttpClient httpClient, MetadataCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
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
                metadata = await ExtractFromPlaywrightAsync(url);
            }
            else
            {
                metadata = await ExtractFromHttpClientAsync(url);
            }

            _cache.Set(url, metadata);
            return metadata;
        }
        catch
        {
            var fallback = GetFallbackMetadata(url);
            _cache.Set(url, fallback);
            return fallback;
        }
    }

    private async Task<(string title, string? description, string? imageUrl)> ExtractFromHttpClientAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return GetFallbackMetadata(url);
        }

        var html = await response.Content.ReadAsStringAsync();
        return ExtractFromHtml(html, url);
    }

    private async Task<(string title, string? description, string? imageUrl)> ExtractFromPlaywrightAsync(string url)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        
        try
        {
            var page = await browser.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 15000
            });

            await Task.Delay(2000);

            var html = await page.ContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = GetMetaContent(doc, "og:title")
                ?? GetMetaContent(doc, "twitter:title")
                ?? GetTitleFromHtml(doc)
                ?? ExtractXTitleFromPlaywright(doc, url);

            var description = GetMetaContent(doc, "og:description")
                ?? GetMetaContent(doc, "twitter:description")
                ?? GetMetaContent(doc, "description")
                ?? ExtractXDescriptionFromPlaywright(doc, url);

            var imageUrl = GetMetaContent(doc, "og:image")
                ?? GetMetaContent(doc, "twitter:image");

            return (title, description, imageUrl);
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private string? ExtractXTitleFromPlaywright(HtmlDocument doc, string url)
    {
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
            ?? ExtractXTitle(doc, url)
            ?? GetDomainFromUrl(url);
        
        return CleanText(title);
    }

    private string? ExtractDescription(HtmlDocument doc, string url)
    {
        var description = GetMetaContent(doc, "og:description")
            ?? GetMetaContent(doc, "twitter:description")
            ?? GetMetaContent(doc, "description")
            ?? ExtractXDescription(doc, url)
            ?? ExtractFirstParagraph(doc);
        
        return description != null ? CleanText(description) : null;
    }

    private string? ExtractImage(HtmlDocument doc, string url)
    {
        var imageUrl = GetMetaContent(doc, "og:image")
            ?? GetMetaContent(doc, "twitter:image")
            ?? GetMetaContent(doc, "twitter:image:src");
        
        return imageUrl != null ? MakeAbsoluteUrl(imageUrl, url) : null;
    }

    private string? ExtractXTitle(HtmlDocument doc, string url)
    {
        if (!IsXUrl(url)) return null;

        var twitterTitle = GetMetaContent(doc, "twitter:title");
        if (!string.IsNullOrEmpty(twitterTitle)) return twitterTitle;

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[@data-testid='UserDescription']");
        if (titleNode != null) return titleNode.InnerText?.Trim();

        var userHandle = ExtractUserHandleFromUrl(url);
        if (!string.IsNullOrEmpty(userHandle))
        {
            var bioNode = doc.DocumentNode.SelectSingleNode($"//div[@data-testid='UserDescription']");
            var userNameNode = doc.DocumentNode.SelectSingleNode("//div[@data-testid='UserName']");
            var handle = userNameNode?.InnerText?.Trim();
            var bio = bioNode?.InnerText?.Trim();
            
            if (!string.IsNullOrEmpty(handle))
            {
                return $"@{handle} - {bio ?? ""}";
            }
        }

        return GetTitleFromHtml(doc);
    }

    private string? ExtractXDescription(HtmlDocument doc, string url)
    {
        if (!IsXUrl(url)) return null;

        var twitterDesc = GetMetaContent(doc, "twitter:description");
        if (!string.IsNullOrEmpty(twitterDesc)) return twitterDesc;

        var tweetNode = doc.DocumentNode.SelectSingleNode("//div[@data-testid='tweet']");
        if (tweetNode != null)
        {
            var textNode = tweetNode.SelectSingleNode(".//div[@data-testid='tweetText']");
            if (textNode != null)
            {
                var text = textNode.InnerText?.Trim();
                return !string.IsNullOrEmpty(text) ? TruncateText(text, 200) : null;
            }
        }

        return ExtractUserDescription(doc);
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
        
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
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
        cleaned = Regex.Replace(cleaned, @"\n+", " ");
        cleaned = cleaned.Replace("\r", "");
        cleaned = cleaned.Replace("\t", " ");
        
        return cleaned;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength).Trim() + "...";
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
