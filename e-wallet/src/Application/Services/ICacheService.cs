namespace e_wallet.Application.Services;

using e_wallet.Application.DTOs.Wallet;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
}
