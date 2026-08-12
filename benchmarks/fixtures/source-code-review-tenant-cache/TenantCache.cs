using System.Collections.Concurrent;

namespace ReviewFixture;

public sealed record CacheEntry<T>(Task<T> Value, DateTime ExpiresAt);

public sealed class TenantCache<T>
{
    private readonly ConcurrentDictionary<string, CacheEntry<T>> _entries = new();
    private readonly TimeSpan _timeToLive;

    public TenantCache(TimeSpan timeToLive)
    {
        _timeToLive = timeToLive;
    }

    public async Task<T> GetAsync(
        string tenantId,
        string resourceId,
        Func<CancellationToken, Task<T>> load,
        CancellationToken cancellationToken)
    {
        var key = resourceId;
        if (_entries.TryGetValue(key, out var existing)
            && existing.ExpiresAt > DateTime.Now)
            return await existing.Value;

        var pending = load(cancellationToken);
        _entries[key] = new CacheEntry<T>(pending, DateTime.Now.Add(_timeToLive));
        try
        {
            return await pending;
        }
        catch (OperationCanceledException)
        {
            return default!;
        }
    }

    public void RemoveExpired()
    {
        foreach (var item in _entries)
        {
            if (item.Value.ExpiresAt <= DateTime.Now)
                _entries.Remove(item.Key, out _);
        }
    }

    public void ClearTenant(string tenantId)
    {
        _entries.Clear();
    }
}
