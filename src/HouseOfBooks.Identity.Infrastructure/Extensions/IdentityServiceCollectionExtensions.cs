namespace HouseOfBooks.Identity.Infrastructure.Extensions;

using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Application.Services;
using HouseOfBooks.Identity.Infrastructure.Idempotency;
using HouseOfBooks.Identity.Infrastructure.Outbox;
using HouseOfBooks.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityOrchestration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core orchestration
        services.AddScoped<IIdentityOrchestrationService,
                           IdentityOrchestrationService>();

        // ── Options ──────────────────────────────────────────────
        services.Configure<IdentityPersistenceOptions>(
            configuration.GetSection(IdentityPersistenceOptions.Section));

        // ── Persistence (Scoped — one instance per request) ──────
        services.AddScoped<IUnitOfWorkFactory, SqlServerUnitOfWorkFactory>();

        // Single UoW instance shared across all repositories in scope.
        // Repositories receive IUnitOfWork; they downcast to access
        // the live Connection + Transaction.
        services.AddScoped<SqlServerUnitOfWork>(sp =>
        {
            var factory = sp.GetRequiredService<IUnitOfWorkFactory>();
            return (SqlServerUnitOfWork)factory.CreateAsync()
                                               .GetAwaiter()
                                               .GetResult();
        });
        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<SqlServerUnitOfWork>());

        services.AddScoped<IUserRepository,          SqlUserRepository>();
        services.AddScoped<IExternalIdentityService, SqlExternalIdentityRepository>();
        services.AddScoped<IOutboxRepository,        SqlOutboxRepository>();
        services.AddScoped<IOutboxRelayRepository,   SqlOutboxRelayRepository>();

        // ── Orchestration (Scoped) ───────────────────────────────
        services.AddScoped<IIdentityOrchestrationService,
                           IdentityOrchestrationService>();

        // ── Idempotency ──────────────────────────────────────────
        // Switch driven by appsettings: Identity:UseRedis = true/false
        // Single-replica dev/staging → InMemory
        // Multi-replica production   → Redis  (requires AddStackExchangeRedisCache)
        var useRedis = configuration.GetValue<bool>("Identity:UseRedis");

        if (useRedis)
        {
            // Caller must have already called:
            //   services.AddStackExchangeRedisCache(o => o.Configuration = "...")
            services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        }
        else
        {
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        }

        // ── Outbox relay background worker (Singleton) ───────────
        services.AddScoped<IOutboxEventProcessor, OutboxEventProcessor>();
        services.AddHostedService<OutboxRelayWorker>();

        return services;
    }
}