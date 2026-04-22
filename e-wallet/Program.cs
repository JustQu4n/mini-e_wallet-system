using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Serilog;
using StackExchange.Redis;
using e_wallet.Application.DTOs.Auth;
using e_wallet.Application.DTOs.Wallet;
using e_wallet.Application.Repositories;
using e_wallet.Application.Services;
using e_wallet.Application.Validators;
using e_wallet.Infrastructure.Caching;
using e_wallet.Infrastructure.Data;
using e_wallet.Infrastructure.Repositories;
using e_wallet.Infrastructure.Security;
using e_wallet.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Make Serilog's logger available via DI for components that accept Serilog.ILogger
builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "E-Wallet API",
        Version = "v1",
        Description = "A complete Mini E-Wallet system with transaction safety and concurrency handling"
    });

    // Add JWT Bearer authentication to Swagger
    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "JWT Authentication",
        Description = "Enter JWT Bearer token",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// FluentValidation configuration
builder.Services.AddValidatorsFromAssemblyContaining(typeof(RegisterRequestValidator));

// Repository and Service registration
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Cache service registration
var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled");
if (redisEnabled)
{
    try
    {
        var redisConnection = builder.Configuration.GetValue<string>("Redis:Connection") 
            ?? throw new InvalidOperationException("Redis connection not configured");
        var redis = ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
    }
    catch (Exception ex)
    {
        Log.Warning("Redis connection failed, falling back to in-memory cache: {Exception}", ex.Message);
        builder.Services.AddScoped<ICacheService, InMemoryCacheService>();
    }
}
else
{
    builder.Services.AddScoped<ICacheService, InMemoryCacheService>();
}

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    try
    {
        // For initial PostgreSQL setup in Docker, ensure the database schema is created
        // This avoids relying on provider-specific migration artifacts in the repo.
        dbContext.Database.EnsureCreated();
        Log.Information("Database ensured/created successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating database schema");
    }
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Wallet API v1");
    options.RoutePrefix = string.Empty;
});

app.UseGlobalExceptionMiddleware();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting E-Wallet API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

