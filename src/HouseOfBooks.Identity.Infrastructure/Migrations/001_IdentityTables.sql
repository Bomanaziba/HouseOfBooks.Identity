-- TABLE: Users
CREATE TABLE Users
(
    UserId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    SchoolId         UNIQUEIDENTIFIER NOT NULL,
    FirstName        NVARCHAR(100)    NOT NULL,
    LastName         NVARCHAR(100)    NOT NULL,
    Email            NVARCHAR(256)    NOT NULL,
    Role             NVARCHAR(50)     NOT NULL,
    AssignedIdentity NVARCHAR(100)    NOT NULL,
    CreatedAtUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Identity_Users          PRIMARY KEY (UserId),
    CONSTRAINT UQ_Identity_Users_Email    UNIQUE (SchoolId, Email),
    CONSTRAINT UQ_Identity_Users_Identity UNIQUE (SchoolId, AssignedIdentity)
);

-- TABLE: ExternalMappings
CREATE TABLE ExternalMappings
(
    MappingId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId             UNIQUEIDENTIFIER NOT NULL,
    SchoolId           UNIQUEIDENTIFIER NOT NULL,
    Role               NVARCHAR(50)     NOT NULL,
    ExternalIdentifier NVARCHAR(200)    NOT NULL,
    CreatedAtUtc       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Identity_ExternalMappings PRIMARY KEY (MappingId),
    CONSTRAINT FK_ExternalMappings_Users    FOREIGN KEY (UserId)
        REFERENCES Users (UserId),

    -- This is the DB-level duplicate guard (defence-in-depth
    -- behind the application-level MappingExistsAsync check)
    CONSTRAINT UQ_ExternalMappings_SchoolExtId
        UNIQUE (SchoolId, ExternalIdentifier)
);

-- TABLE: Outbox  (Step 3 — added in hardening phase)
CREATE TABLE Outbox
(
    OutboxId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    AggregateId   UNIQUEIDENTIFIER NOT NULL,   -- UserId
    EventType     NVARCHAR(100)    NOT NULL,
    Payload       NVARCHAR(MAX)    NOT NULL,   -- JSON
    CreatedAtUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAtUtc DATETIME2       NULL,

    CONSTRAINT PK_Identity_Outbox PRIMARY KEY (OutboxId)
);
CREATE INDEX IX_Outbox_Unprocessed
    ON Outbox (CreatedAtUtc)
    WHERE ProcessedAtUtc IS NULL;


-- STORED PROCEDURES

CREATE OR ALTER PROCEDURE usp_Identity_CreateUser
    @UserId           UNIQUEIDENTIFIER,
    @SchoolId         UNIQUEIDENTIFIER,
    @FirstName        NVARCHAR(100),
    @LastName         NVARCHAR(100),
    @Email            NVARCHAR(256),
    @Role             NVARCHAR(50),
    @AssignedIdentity NVARCHAR(100),
    @CreatedAtUtc     DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Users
        (UserId, SchoolId, FirstName, LastName, Email, Role, AssignedIdentity, CreatedAtUtc)
    VALUES
        (@UserId, @SchoolId, @FirstName, @LastName, @Email, @Role, @AssignedIdentity, @CreatedAtUtc);
END;

CREATE OR ALTER PROCEDURE usp_Identity_PersistExternalMapping
    @MappingId          UNIQUEIDENTIFIER,
    @UserId             UNIQUEIDENTIFIER,
    @SchoolId           UNIQUEIDENTIFIER,
    @Role               NVARCHAR(50),
    @ExternalIdentifier NVARCHAR(200),
    @CreatedAtUtc       DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ExternalMappings
        (MappingId, UserId, SchoolId, Role, ExternalIdentifier, CreatedAtUtc)
    VALUES
        (@MappingId, @UserId, @SchoolId, @Role, @ExternalIdentifier, @CreatedAtUtc);
END;

CREATE OR ALTER PROCEDURE usp_Identity_CheckExternalMappingExists
    @SchoolId           UNIQUEIDENTIFIER,
    @ExternalIdentifier NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1)
    FROM ExternalMappings
    WHERE SchoolId = @SchoolId
      AND ExternalIdentifier = @ExternalIdentifier;
END;

-- Outbox insert (called inside the same transaction as user creation)
CREATE OR ALTER PROCEDURE usp_Identity_InsertOutboxEvent
    @OutboxId    UNIQUEIDENTIFIER,
    @AggregateId UNIQUEIDENTIFIER,
    @EventType   NVARCHAR(100),
    @Payload     NVARCHAR(MAX),
    @CreatedAtUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Outbox (OutboxId, AggregateId, EventType, Payload, CreatedAtUtc)
    VALUES (@OutboxId, @AggregateId, @EventType, @Payload, @CreatedAtUtc);
END;
