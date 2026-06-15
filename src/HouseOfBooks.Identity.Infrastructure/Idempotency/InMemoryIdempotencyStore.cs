namespace HouseOfBooks.Identity.Infrastructure.Idempotency;

using System.Collections.Concurrent;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Models;

/// <summary>
/// Single-instance in-memory store.
/// Swap for a Redis implementation (IDistributedCache) when
/// running multiple API replicas. Interface stays the same.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    // TTL: 24 h is standard for idempotency windows
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, (CreatedUserResult Result, DateTime ExpiresAt)>
        _store = new();

    public Task<CreatedUserResult?> TryGetAsync(
        string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry) &&
            entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<CreatedUserResult?>(entry.Result);
        }
        return Task.FromResult<CreatedUserResult?>(null);
    }

    public Task SetAsync(
        string key, CreatedUserResult result, CancellationToken ct = default)
    {
        _store[key] = (result, DateTime.UtcNow.Add(Ttl));
        return Task.CompletedTask;
    }
}
