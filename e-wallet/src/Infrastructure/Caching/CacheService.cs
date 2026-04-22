namespace e_wallet.Infrastructure.Caching;

using System.Text.Json;
using StackExchange.Redis;
using e_wallet.Application.Services;
using Serilog;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
            {
                _logger.Debug("Cache miss for key: {Key}", key);
                return default;
            }

            _logger.Debug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving from cache: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, serialized, expiration);

            _logger.Debug("Value set in cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);

            _logger.Debug("Cache key removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing cache key: {Key}", key);
        }
    }
}

// Fallback in-memory cache implementation for development without Redis
public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, (object value, DateTime expiration)> _cache = new();
    private readonly ILogger _logger;

    public InMemoryCacheService(ILogger logger)
    {
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (item.expiration > DateTime.UtcNow)
                {
                    _logger.Debug("Cache hit for key: {Key}", key);
                    return Task.FromResult((T?)item.value);
                }

                _cache.Remove(key);
            }

            _logger.Debug("Cache miss for key: {Key}", key);
            return Task.FromResult((T?)default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        lock (_cache)
        {
            var expirationTime = expiration.HasValue
                ? DateTime.UtcNow.Add(expiration.Value)
                : DateTime.UtcNow.AddHours(1);

            _cache[key] = (value!, expirationTime);
            _logger.Debug("Value set in cache: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        lock (_cache)
        {
            _cache.Remove(key);
            _logger.Debug("Cache key removed: {Key}", key);
        }

        return Task.CompletedTask;
    }
}
