namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Cache en mémoire avec TTL configurable pour réduire les appels API.
/// </summary>
public sealed class CacheService
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

    private sealed class CacheEntry
    {
        public object Data { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }

    public T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            return entry.Data as T;

        if (entry?.IsExpired == true)
            _cache.Remove(key);

        return null;
    }

    public void Set<T>(string key, T data, TimeSpan? ttl = null) where T : class
    {
        _cache[key] = new CacheEntry
        {
            Data = data,
            ExpiresAt = DateTime.UtcNow + (ttl ?? _defaultTtl)
        };
    }

    public void Invalidate(string key)
    {
        _cache.Remove(key);
    }

    public void InvalidatePrefix(string prefix)
    {
        var keys = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keys)
            _cache.Remove(key);
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }
}
