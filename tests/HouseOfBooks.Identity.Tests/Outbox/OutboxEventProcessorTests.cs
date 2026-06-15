
namespace HouseOfBooks.Identity.Tests.Outbox;

using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Events;
using HouseOfBooks.Identity.Infrastructure.Outbox;
using HouseOfBooks.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public sealed class OutboxEventProcessorTests
{
    private readonly IOutboxRelayRepository _relay =
        Substitute.For<IOutboxRelayRepository>();

    private OutboxEventProcessor BuildSut() =>
        new(_relay, NullLogger<OutboxEventProcessor>.Instance);

    private static OutboxEvent MakeEvent(string type = "UserCreatedEvent") => new()
    {
        OutboxId    = Guid.NewGuid(),
        AggregateId = Guid.NewGuid(),
        EventType   = type,
        Payload     = "{}"
    };

    [Fact]
    public async Task ProcessPendingAsync_MarksEachEventProcessed()
    {
        var events = new[] { MakeEvent(), MakeEvent() };

        _relay.FetchUnprocessedAsync(50, default)
              .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<OutboxEvent>>(events));

        var sut = BuildSut();
        await sut.ProcessPendingAsync();

        foreach (var evt in events)
            await _relay.Received(1).MarkProcessedAsync(evt.OutboxId, default);
    }

    [Fact]
    public async Task ProcessPendingAsync_OneEventFails_OthersStillProcessed()
    {
        var failing  = MakeEvent();
        var passing  = MakeEvent();
        var events   = new[] { failing, passing };

        _relay.FetchUnprocessedAsync(50, default)
              .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<OutboxEvent>>(events));

        // MarkProcessed throws for the first event only
        _relay.MarkProcessedAsync(failing.OutboxId, default)
              .ThrowsAsyncForAnyArgs(new Exception("transient failure"));

        var sut = BuildSut();
        await sut.ProcessPendingAsync(); // must not throw

        // Passing event still marked
        await _relay.Received(1).MarkProcessedAsync(passing.OutboxId, default);
    }

    [Fact]
    public async Task ProcessPendingAsync_EmptyBatch_DoesNothing()
    {
        _relay.FetchUnprocessedAsync(50, default)
              .ReturnsForAnyArgs(
                  Task.FromResult<IReadOnlyList<OutboxEvent>>(
                      Array.Empty<OutboxEvent>()));

        var sut = BuildSut();
        await sut.ProcessPendingAsync();

        await _relay.DidNotReceiveWithAnyArgs()
                    .MarkProcessedAsync(default, default);
    }
}

