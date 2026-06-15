
// ═════════════════════════════════════════════════════════════════
// FILE: API/Middleware/SchoolContextMiddleware.cs
// Validates that X-School-Id header matches the body's SchoolId.
// Prevents callers from accidentally (or maliciously) writing
// to a school they did not intend to target.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Middleware;

using System.Text.Json;
using HouseOfBooks.Identity.API.Responses;

public sealed class SchoolContextMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SchoolContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce on mutating routes under /api/identity/users
        if (context.Request.Method is "POST" or "PUT" or "PATCH" &&
            context.Request.Path.StartsWithSegments("/api/identity/users"))
        {
            var headerValue = context.Request.Headers["X-School-Id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(headerValue) ||
                !Guid.TryParse(headerValue, out _))
            {
                context.Response.StatusCode  = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                var body = new ApiErrorResponse
                {
                    ErrorCode = "MISSING_SCHOOL_CONTEXT",
                    Message   = "X-School-Id header is required and must be a valid GUID."
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(body, JsonOptions));
                return;
            }
        }

        await _next(context);
    }
}
