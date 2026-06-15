namespace HouseOfBooks.Identity.Tests.Orchestration;

using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Application.Services;
using HouseOfBooks.Identity.Domain.Enums;
using HouseOfBooks.Identity.Domain.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public sealed class IdentityOrchestrationServiceTests
{
    private readonly IIdentityGeneratorService _generator =
        Substitute.For<IIdentityGeneratorService>();
    private readonly IExternalIdentityService _external =
        Substitute.For<IExternalIdentityService>();
    private readonly IUserRepository _repo =
        Substitute.For<IUserRepository>();

    private readonly IOutboxRepository _uobRepo =
        Substitute.For<IOutboxRepository>();

    private readonly IUnitOfWorkFactory _uowFactory =
        Substitute.For<IUnitOfWorkFactory>();

    private readonly IIdempotencyStore _ipStore =
        Substitute.For<IIdempotencyStore>();

    private readonly ILogger<IdentityOrchestrationService> _logger =
        Substitute.For<ILogger<IdentityOrchestrationService>>();

    private IdentityOrchestrationService BuildSut() =>
        new (_generator, _external, _repo, _uobRepo, _uowFactory, _ipStore, _logger);

    private IUnitOfWork ArrangeUow()
    {
        var uow = Substitute.For<IUnitOfWork>();
        _uowFactory.CreateAsync(default).ReturnsForAnyArgs(Task.FromResult(uow));
        return uow;
    }

    [Fact]
    public async Task CreateUserAsync_NormalMode_PersistsUserAndReturnsIdentity()
    {
        // Arrange
        var uow = ArrangeUow();
        var userId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        const string generatedId = "STU/2024/00001";

        var request = new CreateUserRequest
        {
            SchoolId = schoolId,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@school.com",
            Role = RoleCategory.Student
        };

        _generator.GenerateAsync(schoolId, RoleCategory.Student, null, null, default)
                  .ReturnsForAnyArgs(Task.FromResult(generatedId));
        _repo.CreateAsync(Arg.Any<UserRecord>(), default)
             .ReturnsForAnyArgs(Task.FromResult(userId));

        var sut = BuildSut();

        // Act
        var result = await sut.CreateUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(generatedId, result.Data!.AssignedIdentity);
        Assert.False(result.Data.IsMigrated);
        Assert.Null(result.Data.ExternalMappingId);

        await uow.Received(1).CommitAsync(default);
        await _external.DidNotReceiveWithAnyArgs()
                       .PersistMappingAsync(default!, default);
    }

    [Fact]
    public async Task CreateUserAsync_MigrationMode_PersistsMappingAndCommits()
    {
        // Arrange
        var uow = ArrangeUow();
        var userId = Guid.NewGuid();
        var mappingId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        const string extId = "LEGACY-42";

        var request = new CreateUserRequest
        {
            SchoolId = schoolId,
            FirstName = "Alan",
            LastName = "Turing",
            Email = "alan@school.com",
            Role = RoleCategory.StaffAcademic,
            ExternalIdentifier = extId
        };

        _external.MappingExistsAsync(schoolId, extId, default)
                 .ReturnsForAnyArgs(Task.FromResult(false));
        _generator.GenerateAsync(default, default, default, extId, default)
                  .ReturnsForAnyArgs(Task.FromResult(extId));
        _repo.CreateAsync(Arg.Any<UserRecord>(), default)
             .ReturnsForAnyArgs(Task.FromResult(userId));
        _external.PersistMappingAsync(Arg.Any<ExternalIdentityMapping>(), default)
                 .ReturnsForAnyArgs(Task.FromResult(mappingId));

        var sut = BuildSut();

        // Act
        var result = await sut.CreateUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsMigrated);
        Assert.Equal(mappingId, result.Data.ExternalMappingId);
        await uow.Received(1).CommitAsync(default);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateExternalId_ReturnsFailureWithoutPersisting()
    {
        // Arrange
        var schoolId = Guid.NewGuid();
        const string extId = "LEGACY-DUPE";

        _external.MappingExistsAsync(schoolId, extId, default)
                 .ReturnsForAnyArgs(Task.FromResult(true));

        var request = new CreateUserRequest
        {
            SchoolId = schoolId,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@school.com",
            Role = RoleCategory.Student,
            ExternalIdentifier = extId
        };

        var sut = BuildSut();

        // Act
        var result = await sut.CreateUserAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("EXTERNAL_IDENTITY_DUPLICATE", result.ErrorCode);
        await _repo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task CreateUserAsync_RepoThrows_RollsBackAndReturnsFailure()
    {
        // Arrange
        var uow = ArrangeUow();
        var schoolId = Guid.NewGuid();

        _generator.GenerateAsync(default, default, default, default, default)
                  .ReturnsForAnyArgs(Task.FromResult("STU/2024/00099"));
        _repo.CreateAsync(Arg.Any<UserRecord>(), default)
             .ThrowsAsyncForAnyArgs(new InvalidOperationException("DB failure"));

        var request = new CreateUserRequest
        {
            SchoolId = schoolId,
            FirstName = "Test",
            LastName = "User",
            Email = "t@s.com",
            Role = RoleCategory.Student
        };

        var sut = BuildSut();

        // Act
        var result = await sut.CreateUserAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("USER_CREATION_ERROR", result.ErrorCode);
        await uow.Received(1).RollbackAsync();
        await uow.DidNotReceive().CommitAsync(default);
    }
}