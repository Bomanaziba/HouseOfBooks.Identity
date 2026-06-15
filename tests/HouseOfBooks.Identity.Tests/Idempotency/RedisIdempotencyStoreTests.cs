
namespace HouseOfBooks.Identity.Tests.Idempotency;

using System.Text.Json;
using HouseOfBooks.Identity.Domain.Models;
using HouseOfBooks.Identity.Infrastructure.Idempotency;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public sealed class RedisIdempotencyStoreTests
{
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();

    private RedisIdempotencyStore BuildSut() =>
        new(_cache, NullLogger<RedisIdempotencyStore>.Instance);

    private static CreatedUserResult SampleResult() => new()
    {
        UserId           = Guid.NewGuid(),
        AssignedIdentity = "STU/2024/00001",
        IsMigrated       = false,
        ExternalMappingId = null
    };

    [Fact]
    public async Task TryGetAsync_CacheHit_ReturnsDeserializedResult()
    {
        var result  = SampleResult();
        var bytes   = JsonSerializer.SerializeToUtf8Bytes(result);
        const string key = "test-key-001";

        _cache.GetAsync($"identity:idem:{key}", default)
              .ReturnsForAnyArgs(Task.FromResult<byte[]?>(bytes));

        var sut   = BuildSut();
        var found = await sut.TryGetAsync(key);

        Assert.NotNull(found);
        Assert.Equal(result.UserId,           found!.UserId);
        Assert.Equal(result.AssignedIdentity, found.AssignedIdentity);
    }

    [Fact]
    public async Task TryGetAsync_CacheMiss_ReturnsNull()
    {
        _cache.GetAsync(Arg.Any<string>(), default)
              .ReturnsForAnyArgs(Task.FromResult<byte[]?>(null));

        var sut   = BuildSut();
        var found = await sut.TryGetAsync("missing-key");

        Assert.Null(found);
    }

    [Fact]
    public async Task TryGetAsync_RedisThrows_ReturnsNullWithoutPropagating()
    {
        _cache.GetAsync(Arg.Any<string>(), default)
              .ThrowsAsyncForAnyArgs(new Exception("Redis unavailable"));

        var sut   = BuildSut();
        var found = await sut.TryGetAsync("any-key"); // must not throw

        Assert.Null(found);
    }

    [Fact]
    public async Task SetAsync_RedisThrows_DoesNotPropagate()
    {
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                        Arg.Any<DistributedCacheEntryOptions>(), default)
              .ThrowsAsyncForAnyArgs(new Exception("Redis unavailable"));

        var sut = BuildSut();
        var ex  = await Record.ExceptionAsync(() =>
            sut.SetAsync("key", SampleResult()));

        Assert.Null(ex); // swallowed — non-fatal
    }

    [Fact]
    public async Task SetAsync_StoresWithCorrectPrefix()
    {
        const string key = "idem-key-xyz";
        var sut = BuildSut();

        await sut.SetAsync(key, SampleResult());

        await _cache.Received(1).SetAsync(
            $"identity:idem:{key}",
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }
}