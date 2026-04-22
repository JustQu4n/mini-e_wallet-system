namespace e_wallet.API.Middleware;

using System.Net;
using System.Text.Json;
using Serilog;
using e_wallet.Application.DTOs.Common;
using e_wallet.Application.Exceptions;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            UserNotFoundException ex => new
            {
                statusCode = HttpStatusCode.NotFound,
                message = ex.Message
            },
            InsufficientFundsException ex => new
            {
                statusCode = HttpStatusCode.BadRequest,
                message = ex.Message
            },
            DuplicateEmailException ex => new
            {
                statusCode = HttpStatusCode.Conflict,
                message = ex.Message
            },
            InvalidCredentialsException ex => new
            {
                statusCode = HttpStatusCode.Unauthorized,
                message = ex.Message
            },
            ConcurrencyException ex => new
            {
                statusCode = HttpStatusCode.Conflict,
                message = ex.Message
            },
            InvalidOperationException ex => new
            {
                statusCode = HttpStatusCode.BadRequest,
                message = ex.Message
            },
            WalletException ex => new
            {
                statusCode = HttpStatusCode.BadRequest,
                message = ex.Message
            },
            _ => new
            {
                statusCode = HttpStatusCode.InternalServerError,
                message = "An unexpected error occurred"
            }
        };

        context.Response.StatusCode = (int)response.statusCode;

        var apiResponse = new ApiResponse
        {
            Success = false,
            Message = response.message
        };

        return context.Response.WriteAsJsonAsync(apiResponse);
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
