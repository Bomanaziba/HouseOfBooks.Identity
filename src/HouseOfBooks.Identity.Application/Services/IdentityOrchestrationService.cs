
namespace HouseOfBooks.Identity.Application.Services;

using System.Text.Json;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Common;
using HouseOfBooks.Identity.Domain.Events;
using HouseOfBooks.Identity.Domain.Models;
using Microsoft.Extensions.Logging;

public sealed class IdentityOrchestrationService : IIdentityOrchestrationService
{
    private readonly IIdentityGeneratorService _identityGenerator;
    private readonly IExternalIdentityService _externalIdentityService;
    private readonly IUserRepository _userRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<IdentityOrchestrationService> _logger;

    public IdentityOrchestrationService(
        IIdentityGeneratorService identityGenerator,
        IExternalIdentityService externalIdentityService,
        IUserRepository userRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWorkFactory uowFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<IdentityOrchestrationService> logger)
    {
        _identityGenerator = identityGenerator;
        _externalIdentityService = externalIdentityService;
        _userRepository = userRepository;
        _outboxRepository = outboxRepository;
        _uowFactory = uowFactory;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public async Task<ServiceResult<CreatedUserResult>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken ct = default)
    {
        // ── 1. Idempotency short-circuit ─────────────────────
        if (request.IdempotencyKey is not null)
        {
            var cached = await _idempotencyStore.TryGetAsync(
                request.IdempotencyKey, ct);

            if (cached is not null)
            {
                _logger.LogInformation(
                    "Idempotent replay for key {Key}. Returning cached result.",
                    request.IdempotencyKey);
                return ServiceResult<CreatedUserResult>.Success(cached);
            }
        }

        // ── 2. Validate ──────────────────────────────────────
        var validation = ValidateRequest(request);
        if (!validation.IsSuccess) return validation;

        var ctx = new IdentityOrchestrationContext { Request = request };

        // ── 3. Duplicate external mapping guard ──────────────
        if (ctx.IsMigrationMode)
        {
            var exists = await _externalIdentityService.MappingExistsAsync(
                request.SchoolId, request.ExternalIdentifier!, ct);

            if (exists)
                return ServiceResult<CreatedUserResult>.Failure(
                    $"External identifier '{request.ExternalIdentifier}' is already " +
                    $"mapped for school {request.SchoolId}.",
                    "EXTERNAL_IDENTITY_DUPLICATE");
        }

        // ── 4. Resolve identity (outside transaction) ────────
        try
        {
            ctx.ResolvedIdentity = await _identityGenerator.GenerateAsync(
                schoolId: request.SchoolId,
                role: request.Role,
                context: request.IdentityContext,
                externalIdentifier: request.ExternalIdentifier,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Identity generation failed for school {SchoolId}, role {Role}",
                request.SchoolId, request.Role);
            return ServiceResult<CreatedUserResult>.Failure(
                "Identity generation failed.", "IDENTITY_GENERATION_ERROR");
        }

        // ── 5. Atomic persistence block ──────────────────────
        await using var uow = await _uowFactory.CreateAsync(ct);

        try
        {
            await uow.BeginAsync(ct);

            // 5a. Persist user
            var userRecord = BuildUserRecord(ctx);
            ctx.UserId = await _userRepository.CreateAsync(userRecord, ct);

            // 5b. Persist external mapping (migration mode only)
            Guid? mappingId = null;
            if (ctx.IsMigrationMode)
                mappingId = await PersistExternalMappingAsync(ctx, ct);

            // 5c. Write outbox event — SAME transaction.
            //     If commit fails, the event is also rolled back.
            //     If commit succeeds, the event is guaranteed to exist.
            //     The background relay picks it up and does the rest.
            await _outboxRepository.InsertAsync(
                BuildOutboxEvent(ctx, mappingId), ct);

            await uow.CommitAsync(ct);

            var result = new CreatedUserResult
            {
                UserId = ctx.UserId,
                AssignedIdentity = ctx.ResolvedIdentity!,
                IsMigrated = ctx.IsMigrationMode,
                ExternalMappingId = mappingId
            };

            // 5d. Cache result for idempotency replay
            if (request.IdempotencyKey is not null)
                await _idempotencyStore.SetAsync(
                    request.IdempotencyKey, result, ct);

            _logger.LogInformation(
                "User created. UserId={UserId} Identity={Identity} " +
                "School={SchoolId} Role={Role} Migrated={Migrated}",
                ctx.UserId, ctx.ResolvedIdentity,
                request.SchoolId, request.Role, ctx.IsMigrationMode);

            return ServiceResult<CreatedUserResult>.Success(result);
        }
        catch (Exception ex)
        {
            await TryRollbackAsync(uow, ex, ctx);
            return ServiceResult<CreatedUserResult>.Failure(
                "User creation failed. No data was saved.",
                "USER_CREATION_ERROR");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static ServiceResult<CreatedUserResult> ValidateRequest(
        CreateUserRequest request)
    {
        if (request.SchoolId == Guid.Empty)
            return Fail("SchoolId is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return Fail("Email is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
            return Fail("Full name is required.");

        return ServiceResult<CreatedUserResult>.Success(null!);

        static ServiceResult<CreatedUserResult> Fail(string msg) =>
            ServiceResult<CreatedUserResult>.Failure(msg, "VALIDATION_ERROR");
    }

    private static UserRecord BuildUserRecord(IdentityOrchestrationContext ctx) =>
        new()
        {
            SchoolId = ctx.Request.SchoolId,
            FirstName = ctx.Request.FirstName,
            LastName = ctx.Request.LastName,
            Email = ctx.Request.Email,
            Role = ctx.Request.Role,
            AssignedIdentity = ctx.ResolvedIdentity!
        };

    private async Task<Guid> PersistExternalMappingAsync(
        IdentityOrchestrationContext ctx,
        CancellationToken ct)
    {
        var mapping = new ExternalIdentityMapping
        {
            UserId = ctx.UserId,
            SchoolId = ctx.Request.SchoolId,
            Role = ctx.Request.Role,
            ExternalIdentifier = ctx.Request.ExternalIdentifier!
        };
        return await _externalIdentityService.PersistMappingAsync(mapping, ct);
    }

    private static OutboxEvent BuildOutboxEvent(
        IdentityOrchestrationContext ctx,
        Guid? mappingId)
    {
        var domainEvent = new UserCreatedEvent(
            UserId: ctx.UserId,
            SchoolId: ctx.Request.SchoolId,
            AssignedIdentity: ctx.ResolvedIdentity!,
            Role: ctx.Request.Role.ToString(),
            IsMigrated: ctx.IsMigrationMode,
            ExternalIdentifier: ctx.Request.ExternalIdentifier,
            OccurredAtUtc: DateTime.UtcNow);

        return new OutboxEvent
        {
            AggregateId = ctx.UserId,
            EventType = nameof(UserCreatedEvent),
            Payload = JsonSerializer.Serialize(domainEvent)
        };
    }

    private async Task TryRollbackAsync(
        IUnitOfWork uow,
        Exception cause,
        IdentityOrchestrationContext ctx)
    {
        _logger.LogError(cause,
            "User creation failed for school {SchoolId}. Rolling back.",
            ctx.Request.SchoolId);
        try
        {
            await uow.RollbackAsync();
        }
        catch (Exception rbEx)
        {
            _logger.LogCritical(rbEx,
                "ROLLBACK FAILED for school {SchoolId}. Manual cleanup required.",
                ctx.Request.SchoolId);
        }
    }
}