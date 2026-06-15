
namespace HouseOfBooks.Identity.Tests.API;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseOfBooks.Identity.API.Requests;
using HouseOfBooks.Identity.API.Responses;
using HouseOfBooks.Identity.Application.Services;
using HouseOfBooks.Identity.Domain.Common;
using HouseOfBooks.Identity.Domain.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public sealed class IdentityControllerTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IdentityControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private HttpClient BuildClient(
        Action<IServiceCollection>? overrides = null)
    {
        return _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                overrides?.Invoke(services);
            });
        }).CreateClient();
    }

    private static IIdentityOrchestrationService MockOrchestration(
        ServiceResult<CreatedUserResult> returnValue)
    {
        var svc = Substitute.For<IIdentityOrchestrationService>();
        svc.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(returnValue));
        return svc;
    }

    private static CreateUserHttpRequest ValidRequest() => new()
    {
        SchoolId  = Guid.NewGuid(),
        FirstName = "Ada",
        LastName  = "Lovelace",
        Email     = "ada@school.com",
        Role      = "Student"
    };

    // ── Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task POST_ValidRequest_Returns201WithLocation()
    {
        var domainResult = new CreatedUserResult
        {
            UserId           = Guid.NewGuid(),
            AssignedIdentity = "STU/2024/00001",
            IsMigrated       = false
        };

        var client = BuildClient(services =>
        {
            services.AddScoped(_ =>
                MockOrchestration(ServiceResult<CreatedUserResult>.Success(domainResult)));
        });

        client.DefaultRequestHeaders.Add(
            "X-School-Id", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/identity/users", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content
            .ReadFromJsonAsync<CreateUserHttpResponse>(JsonOpts);

        Assert.Equal(domainResult.UserId,           body!.UserId);
        Assert.Equal(domainResult.AssignedIdentity, body.AssignedIdentity);
        Assert.False(body.IsMigrated);
    }

    [Fact]
    public async Task POST_DuplicateExternalId_Returns409()
    {
        var client = BuildClient(services =>
        {
            services.AddScoped(_ => MockOrchestration(
                ServiceResult<CreatedUserResult>.Failure(
                    "Duplicate external identifier.",
                    "EXTERNAL_IDENTITY_DUPLICATE")));
        });

        client.DefaultRequestHeaders.Add(
            "X-School-Id", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/identity/users", ValidRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<ApiErrorResponse>(JsonOpts);

        Assert.Equal("EXTERNAL_IDENTITY_DUPLICATE", body!.ErrorCode);
    }

    [Fact]
    public async Task POST_InvalidRole_Returns422WithoutCallingOrchestration()
    {
        var orchestration = Substitute.For<IIdentityOrchestrationService>();

        var client = BuildClient(services =>
        {
            services.AddScoped(_ => orchestration);
        });

        client.DefaultRequestHeaders.Add(
            "X-School-Id", Guid.NewGuid().ToString());

        var badRequest = ValidRequest() with { Role = "Wizard" };
        var response   = await client.PostAsJsonAsync("/api/identity/users", badRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // Mapping failure — orchestration must never be touched
        await orchestration.DidNotReceiveWithAnyArgs()
            .CreateUserAsync(default!, default);
    }

    [Fact]
    public async Task POST_MissingSchoolIdHeader_Returns400()
    {
        var client = BuildClient(); // no X-School-Id header

        var response = await client.PostAsJsonAsync("/api/identity/users", ValidRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<ApiErrorResponse>(JsonOpts);

        Assert.Equal("MISSING_SCHOOL_CONTEXT", body!.ErrorCode);
    }

    [Fact]
    public async Task POST_IdempotencyKey_IsForwardedToOrchestration()
    {
        const string idemKey = "test-idem-key-xyz";

        CreateUserRequest? captured = null;
        var orchestration = Substitute.For<IIdentityOrchestrationService>();
        orchestration
            .CreateUserAsync(Arg.Do<CreateUserRequest>(r => captured = r),
                             Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<CreatedUserResult>.Success(
                new CreatedUserResult
                {
                    UserId           = Guid.NewGuid(),
                    AssignedIdentity = "STU/2024/00001",
                    IsMigrated       = false
                })));

        var client = BuildClient(services =>
        {
            services.AddScoped(_ => orchestration);
        });

        client.DefaultRequestHeaders.Add("X-School-Id",     Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", idemKey);

        await client.PostAsJsonAsync("/api/identity/users", ValidRequest());

        Assert.Equal(idemKey, captured?.IdempotencyKey);
    }

    [Fact]
    public async Task POST_OrchestratorThrowsUnhandled_Returns500WithEnvelope()
    {
        var orchestration = Substitute.For<IIdentityOrchestrationService>();
        orchestration
            .CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new Exception("unexpected boom"));

        var client = BuildClient(services =>
        {
            services.AddScoped(_ => orchestration);
        });

        client.DefaultRequestHeaders.Add("X-School-Id", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/identity/users", ValidRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<ApiErrorResponse>(JsonOpts);

        // GlobalExceptionMiddleware envelope — no stack trace leaked
        Assert.Equal("INTERNAL_ERROR", body!.ErrorCode);
    }
}
