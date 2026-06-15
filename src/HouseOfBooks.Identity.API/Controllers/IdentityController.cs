
// ═════════════════════════════════════════════════════════════════
// FILE: API/Controllers/IdentityController.cs
// Thin. Maps → delegates → maps. No business logic.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Controllers;

using HouseOfBooks.Identity.API.Mapping;
using HouseOfBooks.Identity.API.Requests;
using HouseOfBooks.Identity.API.Responses;
using HouseOfBooks.Identity.Application.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/identity")]
[Produces("application/json")]
public sealed class IdentityController : ControllerBase
{
    private readonly IIdentityOrchestrationService _orchestration;

    public IdentityController(IIdentityOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    /// <summary>
    /// Creates a new user and assigns a system-generated or
    /// externally-supplied identity.
    ///
    /// Pass an <c>Idempotency-Key</c> header (UUID v4 recommended)
    /// to make the call safe to retry. Duplicate requests with the
    /// same key within 24 h return the original result unchanged.
    /// </summary>
    /// <response code="201">User created. Location header points to the new resource.</response>
    /// <response code="409">Duplicate external identifier or idempotency replay.</response>
    /// <response code="422">Validation error (invalid role, missing field, etc.).</response>
    /// <response code="500">Internal error. No data was persisted.</response>
    [HttpPost("users")]
    [ProducesResponseType(typeof(CreateUserHttpResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),       StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse),       StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse),       StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUserAsync(
        [FromBody]   CreateUserHttpRequest httpRequest,
        CancellationToken                 ct = default)
    {
        // ── 1. Extract idempotency key from header ────────────
        var idempotencyKey = Request.Headers["Idempotency-Key"]
                                    .FirstOrDefault();

        // ── 2. Map HTTP → domain (role parsing lives here) ───
        if (!IdentityRequestMapper.TryMapToCreateUserRequest(
                httpRequest, idempotencyKey,
                out var domainRequest, out var mapError))
        {
            return UnprocessableEntity(new ApiErrorResponse
            {
                ErrorCode = "VALIDATION_ERROR",
                Message   = mapError!
            });
        }

        // ── 3. Delegate entirely to orchestration layer ───────
        var result = await _orchestration.CreateUserAsync(domainRequest, ct);

        // ── 4. Map domain result → HTTP response ──────────────
        if (!result.IsSuccess)
            return MapFailureToHttpResult(result.ErrorCode!, result.ErrorMessage!);

        var response = IdentityRequestMapper.MapToHttpResponse(result.Data!);

        return CreatedAtAction(
            actionName:  nameof(GetUserByIdAsync),
            routeValues: new { userId = response.UserId },
            value:       response);
    }

    /// <summary>
    /// Returns a previously created user by their internal UserId.
    /// Used as the Location target after a successful POST.
    /// </summary>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(CreateUserHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),       StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByIdAsync(
        Guid              userId,
        CancellationToken ct = default)
    {
        // Placeholder — wired once IUserReadRepository is built.
        // Exists now so CreatedAtAction above resolves at startup.
        await Task.CompletedTask;
        return NotFound(new ApiErrorResponse
        {
            ErrorCode = "NOT_IMPLEMENTED",
            Message   = "User read endpoint is not yet available."
        });
    }

    // ── Error code → HTTP status mapping ─────────────────────────

    private IActionResult MapFailureToHttpResult(string errorCode, string message)
    {
        var body = new ApiErrorResponse { ErrorCode = errorCode, Message = message };

        return errorCode switch
        {
            "EXTERNAL_IDENTITY_DUPLICATE" => Conflict(body),
            "VALIDATION_ERROR"            => UnprocessableEntity(body),
            "IDENTITY_GENERATION_ERROR"   => StatusCode(StatusCodes.Status500InternalServerError, body),
            "USER_CREATION_ERROR"         => StatusCode(StatusCodes.Status500InternalServerError, body),
            _                             => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }
}
