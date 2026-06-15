
namespace HouseOfBooks.Identity.Infrastructure.Idempotency;

using System.Text.Json;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    // Must match whatever window the API documents to callers.
    // 24 h is the industry standard for payment/identity APIs.
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    // Key prefix isolates identity idempotency entries from
    // other modules that might share the same Redis instance.
    private const string KeyPrefix = "identity:idem:";

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisIdempotencyStore> _logger;

    public RedisIdempotencyStore(
        IDistributedCache cache,
        ILogger<RedisIdempotencyStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<CreatedUserResult?> TryGetAsync(
        string key,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(PrefixedKey(key), ct);
            if (bytes is null) return null;

            return JsonSerializer.Deserialize<CreatedUserResult>(bytes);
        }
        catch (Exception ex)
        {
            // Redis unavailability must NEVER block user creation.
            // Log and fall through — the orchestrator will re-execute
            // (safe: DB unique constraints prevent actual duplicates).
            _logger.LogWarning(ex,
                "Redis idempotency read failed for key {Key}. " +
                "Proceeding without cache.", key);
            return null;
        }
    }

    public async Task SetAsync(
        string key,
        CreatedUserResult result,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Ttl
            };

            await _cache.SetAsync(PrefixedKey(key), bytes, options, ct);
        }
        catch (Exception ex)
        {
            // A write failure here is non-fatal: the user was already
            // committed. The only consequence is that a retry within
            // the TTL window re-executes and hits the DB unique
            // constraint, returning an appropriate error to the caller.
            _logger.LogWarning(ex,
                "Redis idempotency write failed for key {Key}. " +
                "Idempotency protection degraded for this request.", key);
        }
    }

    private static string PrefixedKey(string key) => $"{KeyPrefix}{key}";
}