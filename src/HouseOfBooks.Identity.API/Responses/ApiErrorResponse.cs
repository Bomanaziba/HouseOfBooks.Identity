// ═════════════════════════════════════════════════════════════════
// FILE: API/Responses/ApiErrorResponse.cs
// Uniform error envelope returned on all non-2xx responses.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Responses;

public sealed record ApiErrorResponse
{
    public required string ErrorCode    { get; init; }
    public required string Message      { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
}
