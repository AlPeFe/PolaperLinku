namespace PolaperLinku.Api.Services;

public class MetadataCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

    public bool TryGet(string url, out (string title, string? description, string? imageUrl) metadata)
    {
        var key = GetCacheKey(url);
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < _cacheDuration)
            {
                metadata = entry.Metadata;
                return true;
            }
            _cache.Remove(key);
        }

        metadata = (string.Empty, null, null);
        return false;
    }

    public void Set(string url, (string title, string? description, string? imageUrl) metadata)
    {
        var key = GetCacheKey(url);
        _cache[key] = new CacheEntry
        {
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        };
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private string GetCacheKey(string url)
    {
        return url.ToLowerInvariant().TrimEnd('/');
    }

    private class CacheEntry
    {
        public (string title, string? description, string? imageUrl) Metadata { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
