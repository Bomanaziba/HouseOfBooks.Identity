using HouseOfBooks.Identity.Domain.Common;
using HouseOfBooks.Identity.Domain.Models;

namespace HouseOfBooks.Identity.Application.Services;


/// <summary>
/// Entry point for all user-creation flows.
/// Coordinates identity generation, user persistence,
/// and optional external mapping — atomically.
/// </summary>
public interface IIdentityOrchestrationService
{
    /// <summary>
    /// Creates a user with a system-generated or externally-supplied
    /// identity. The operation is fully atomic: either everything
    /// persists or nothing does.
    /// </summary>
    Task<ServiceResult<CreatedUserResult>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken ct = default);
}

