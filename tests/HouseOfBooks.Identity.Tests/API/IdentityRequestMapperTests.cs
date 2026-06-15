
namespace HouseOfBooks.Identity.Tests.API;

using HouseOfBooks.Identity.API.Mapping;
using HouseOfBooks.Identity.API.Requests;
using HouseOfBooks.Identity.Domain.Enums;
using Xunit;

public sealed class IdentityRequestMapperTests
{
    private static CreateUserHttpRequest BaseRequest(string role = "Student") => new()
    {
        SchoolId  = Guid.NewGuid(),
        FirstName = "Ada",
        LastName  = "Lovelace",
        Email     = "ada@school.com",
        Role      = role
    };

    [Theory]
    [InlineData("Student",         RoleCategory.Student)]
    [InlineData("student",         RoleCategory.Student)]   // case-insensitive
    [InlineData("StaffAcademic",   RoleCategory.StaffAcademic)]
    [InlineData("Lecturer",        RoleCategory.Lecturer)]
    [InlineData("Parent",          RoleCategory.Parent)]
    public void TryMap_ValidRole_ReturnsTrueAndCorrectRole(
        string role, RoleCategory expected)
    {
        var ok = IdentityRequestMapper.TryMapToCreateUserRequest(
            BaseRequest(role), idempotencyKey: null,
            out var result, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, result.Role);
    }

    [Fact]
    public void TryMap_InvalidRole_ReturnsFalseWithMessage()
    {
        var ok = IdentityRequestMapper.TryMapToCreateUserRequest(
            BaseRequest("Headmaster"), idempotencyKey: null,
            out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("Headmaster", error);
    }

    [Fact]
    public void TryMap_IdempotencyKey_IsForwardedToDomainRequest()
    {
        const string key = "idem-abc-123";

        IdentityRequestMapper.TryMapToCreateUserRequest(
            BaseRequest(), idempotencyKey: key,
            out var result, out _);

        Assert.Equal(key, result.IdempotencyKey);
    }

    [Fact]
    public void TryMap_ExternalIdentifier_IsForwarded()
    {
        var req = BaseRequest() with { ExternalIdentifier = "LEGACY-42" };

        IdentityRequestMapper.TryMapToCreateUserRequest(
            req, idempotencyKey: null,
            out var result, out _);

        Assert.Equal("LEGACY-42", result.ExternalIdentifier);
    }

    [Fact]
    public void MapToHttpResponse_FieldsMatchExactly()
    {
        var domainResult = new HouseOfBooks.Identity.Domain.Models.CreatedUserResult
        {
            UserId            = Guid.NewGuid(),
            AssignedIdentity  = "STU/2024/00001",
            IsMigrated        = true,
            ExternalMappingId = Guid.NewGuid()
        };

        var response = IdentityRequestMapper.MapToHttpResponse(domainResult);

        Assert.Equal(domainResult.UserId,            response.UserId);
        Assert.Equal(domainResult.AssignedIdentity,  response.AssignedIdentity);
        Assert.Equal(domainResult.IsMigrated,        response.IsMigrated);
        Assert.Equal(domainResult.ExternalMappingId, response.ExternalMappingId);
    }
}