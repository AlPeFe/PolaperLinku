using HtmlAgilityPack;
using Microsoft.Playwright;
using System.Net;
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
                // Para X (Twitter), usar vxtwitter como proxy para obtener metadatos
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
        // X (Twitter) requiere JavaScript para cargar el contenido
        // Usar vxtwitter como proxy para obtener metadata sin el mensaje "Redirecting you to the tweet in a moment."
        (string title, string? description, string? imageUrl) metadata = ("", null, null);
        try
        {
            var vxUrl = url
                .Replace("twitter.com", "vxtwitter.com")
                .Replace("x.com", "vxtwitter.com");

            _logger?.LogInformation("Fetching metadata from vxtwitter: {VxUrl}", vxUrl);
            
            var response = await _httpClient.GetAsync(vxUrl);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                metadata = ExtractFromHtml(html, url);
                
                _logger?.LogInformation("vxtwitter returned - Title: {Title}, HasDescription: {HasDesc}, HasImage: {HasImage}", 
                    metadata.title, !string.IsNullOrEmpty(metadata.description), !string.IsNullOrEmpty(metadata.imageUrl));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "vxtwitter extraction failed for {Url}", url);
        }

        // Si tenemos título y descripción de vxtwitter, intentar extraer la imagen con Playwright
        if (!string.IsNullOrEmpty(metadata.description) ||
            (!string.IsNullOrEmpty(metadata.title) && 
             !metadata.title.Equals(GetDomainFromUrl(url), StringComparison.OrdinalIgnoreCase)))
        {
            // Si no tenemos imagen, intentar obtenerla con timeout corto
            if (string.IsNullOrEmpty(metadata.imageUrl))
            {
                _logger?.LogInformation("Attempting quick image extraction for {Url}", url);
                
                try
                {
                    // Intentar obtener la imagen con timeout de 7 segundos
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7));
                    var imageTask = ExtractTwitterImageWithPlaywrightAsync(url);
                    var completedTask = await Task.WhenAny(imageTask, Task.Delay(7000, cts.Token));
                    
                    if (completedTask == imageTask)
                    {
                        var imageUrl = await imageTask;
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            metadata = (metadata.title, metadata.description, imageUrl);
                            _logger?.LogInformation("Successfully extracted image in time: {ImageUrl}", imageUrl);
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("Image extraction timeout, continuing in background");
                        // Continuar en background sin esperar
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var bgImageUrl = await imageTask;
                                if (!string.IsNullOrEmpty(bgImageUrl))
                                {
                                    _cache.Set(url, (metadata.title, metadata.description, bgImageUrl));
                                    _logger?.LogInformation("Background: Image extracted and cached: {ImageUrl}", bgImageUrl);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Background image extraction failed");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Quick image extraction failed, skipping");
                }
            }
            return metadata;
        }

        // Fallback: crear metadata a partir de la URL
        _logger?.LogInformation("Using enhanced fallback metadata for X/Twitter URL: {Url}", url);
        return GetEnhancedTwitterFallback(url);
    }

    private (string title, string? description, string? imageUrl) GetEnhancedTwitterFallback(string url)
    {
        var handle = ExtractUserHandleFromUrl(url);
        var title = !string.IsNullOrEmpty(handle) 
            ? $"Post by @{handle} on X" 
            : "Post on X";
        
        return (title, "View this post on X (formerly Twitter)", null);
    }

    private async Task<string?> ExtractTwitterImageWithPlaywrightAsync(string url)
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

            // X nunca alcanza NetworkIdle, usar DOMContentLoaded en su lugar
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            // Esperar a que se carguen las imágenes
            await Task.Delay(3000);

            string? imageUrl = null;

            // Determinar si es un perfil o un tweet
            if (IsTwitterProfile(url))
            {
                // Para perfiles, buscar la imagen del banner
                _logger?.LogInformation("Extracting profile banner image");
                
                // Buscar el banner con varios selectores posibles
                var bannerSelectors = new[]
                {
                    "img[src*='profile_banners']",
                    "a[href*='header_photo'] img",
                    "[data-testid='UserProfileHeader_Items'] img[src*='pbs.twimg.com']"
                };

                foreach (var selector in bannerSelectors)
                {
                    try
                    {
                        var element = await page.QuerySelectorAsync(selector);
                        if (element != null)
                        {
                            imageUrl = await element.GetAttributeAsync("src");
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                _logger?.LogInformation("Found banner image with selector {Selector}: {ImageUrl}", selector, imageUrl);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Continuar con el siguiente selector
                    }
                }

                // Si no hay banner, usar la imagen de perfil
                if (string.IsNullOrEmpty(imageUrl))
                {
                    imageUrl = await ExtractProfileImageAsync(page);
                }
            }
            else
            {
                // Para tweets, buscar la imagen de perfil del autor
                _logger?.LogInformation("Extracting tweet author profile image");
                imageUrl = await ExtractProfileImageAsync(page);
            }

            return imageUrl;
        }
        catch(Exception ex) 
        {
            _logger?.LogError(ex, "Error extracting Twitter image with Playwright for {Url}", url);
            return null;
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private async Task<string?> ExtractProfileImageAsync(IPage page)
    {
        var profileImageSelectors = new[]
        {
            "img[src*='profile_images']",
            "[data-testid='UserAvatar-Container'] img",
            "a[href*='/photo'] img[src*='pbs.twimg.com']"
        };

        foreach (var selector in profileImageSelectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var src = await element.GetAttributeAsync("src");
                    if (!string.IsNullOrEmpty(src) && src.Contains("profile_images"))
                    {
                        // Obtener la versión de mayor calidad (remover _normal, _bigger, etc.)
                        src = src.Replace("_normal", "_400x400").Replace("_bigger", "_400x400");
                        _logger?.LogInformation("Found profile image with selector {Selector}: {ImageUrl}", selector, src);
                        return src;
                    }
                }
            }
            catch
            {
                // Continuar con el siguiente selector
            }
        }

        return null;
    }

    private bool IsTwitterProfile(string url)
    {
        // Un perfil de X no contiene /status/ en la URL
        return !url.Contains("/status/", StringComparison.OrdinalIgnoreCase);
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
                title = $"@{handle} - X";
            }
        }

        return (title, description, null);
    }
}
