
// ═════════════════════════════════════════════════════════════════
// FILE: API/Middleware/GlobalExceptionMiddleware.cs
// Last-resort handler. Catches anything the controller didn't.
// Returns the same ApiErrorResponse envelope — never a stack trace.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Middleware;

using System.Text.Json;
using HouseOfBooks.Identity.API.Responses;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(
        RequestDelegate                      next,
        ILogger<GlobalExceptionMiddleware>   logger)
    {
        _next   = next;
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
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var body = new ApiErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message   = "An unexpected error occurred. Please try again later."
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}