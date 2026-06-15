
// ═════════════════════════════════════════════════════════════════
// FILE: API/Mapping/IdentityRequestMapper.cs
// All mapping logic in one place.
// Controller calls mapper — never maps inline.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Mapping;

using HouseOfBooks.Identity.API.Requests;
using HouseOfBooks.Identity.API.Responses;
using HouseOfBooks.Identity.Domain.Enums;
using HouseOfBooks.Identity.Domain.Models;

public static class IdentityRequestMapper
{
    /// <summary>
    /// Maps the HTTP request + idempotency header to the domain model.
    /// Returns null in <paramref name="error"/> on success,
    /// or a validation message when the Role string is unrecognised.
    /// </summary>
    public static bool TryMapToCreateUserRequest(
        CreateUserHttpRequest httpRequest,
        string?               idempotencyKey,
        out CreateUserRequest domainRequest,
        out string?           error)
    {
        if (!Enum.TryParse<RoleCategory>(httpRequest.Role, ignoreCase: true, out var role))
        {
            domainRequest = null!;
            error         = $"'{httpRequest.Role}' is not a valid role. " +
                            $"Accepted values: {string.Join(", ", Enum.GetNames<RoleCategory>())}";
            return false;
        }

        domainRequest = new CreateUserRequest
        {
            SchoolId           = httpRequest.SchoolId,
            FirstName          = httpRequest.FirstName,
            LastName           = httpRequest.LastName,
            Email              = httpRequest.Email,
            Role               = role,
            ExternalIdentifier = httpRequest.ExternalIdentifier,
            IdentityContext    = httpRequest.IdentityContext,
            IdempotencyKey     = idempotencyKey
        };

        error = null;
        return true;
    }

    public static CreateUserHttpResponse MapToHttpResponse(CreatedUserResult result) =>
        new()
        {
            UserId            = result.UserId,
            AssignedIdentity  = result.AssignedIdentity,
            IsMigrated        = result.IsMigrated,
            ExternalMappingId = result.ExternalMappingId
        };
}
