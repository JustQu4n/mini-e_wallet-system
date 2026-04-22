namespace e_wallet.Application.Services;

using e_wallet.Application.DTOs.Auth;

public interface IAuthService
{
    Task<AuthTokenResponse> RegisterAsync(RegisterRequest request);
    Task<AuthTokenResponse> LoginAsync(LoginRequest request);
}
