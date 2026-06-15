
-- ── Fetch a locked batch of unprocessed events ──────────────────
--
-- UPDLOCK + READPAST: multiple relay workers can run concurrently
-- without reading the same rows. Safe for future scale-out.

CREATE OR ALTER PROCEDURE usp_Identity_FetchUnprocessedOutboxEvents
    @BatchSize INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@BatchSize)
        OutboxId,
        AggregateId,
        EventType,
        Payload,
        CreatedAtUtc
    FROM Identity.Outbox WITH (UPDLOCK, READPAST)
    WHERE ProcessedAtUtc IS NULL
    ORDER BY CreatedAtUtc ASC;
END;

-- ── Mark a single event as processed ────────────────────────────

CREATE OR ALTER PROCEDURE usp_Identity_MarkOutboxEventProcessed
    @OutboxId       UNIQUEIDENTIFIER,
    @ProcessedAtUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Identity.Outbox
    SET    ProcessedAtUtc = @ProcessedAtUtc
    WHERE  OutboxId = @OutboxId;
END;
