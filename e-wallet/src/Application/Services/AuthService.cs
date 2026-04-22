namespace e_wallet.Application.Services;

using Serilog;
using e_wallet.Application.DTOs.Auth;
using e_wallet.Application.Exceptions;
using e_wallet.Application.Repositories;
using e_wallet.Domain.Entities;
using e_wallet.Infrastructure.Security;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger _logger;

    public AuthService(IUserRepository userRepository, IJwtTokenService tokenService, ILogger logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthTokenResponse> RegisterAsync(RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.Warning("Registration attempt with existing email: {Email}", request.Email);
                throw new DuplicateEmailException(request.Email);
            }

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Balance = 0,
                Version = 1
            };

            await _userRepository.CreateAsync(user);

            _logger.Information("User registered successfully: {Email}", request.Email);

            // Generate token
            var token = _tokenService.GenerateToken(user);

            return new AuthTokenResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Token = token,
                ExpiresAt = _tokenService.GetTokenExpirationTime()
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during registration");
            throw;
        }
    }

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.Warning("Failed login attempt: {Email}", request.Email);
                throw new InvalidCredentialsException();
            }

            _logger.Information("User logged in successfully: {Email}", request.Email);

            // Generate token
            var token = _tokenService.GenerateToken(user);

            return new AuthTokenResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Token = token,
                ExpiresAt = _tokenService.GetTokenExpirationTime()
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during login");
            throw;
        }
    }
}
