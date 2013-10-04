CREATE TABLE Message2(
	PivotId bigint NOT NULL,
	FactId bigint NOT NULL,
	ClientId int NOT NULL,
	AncestorFactId bigint NOT NULL,
	AncestorRoleId int NOT NULL,
	CONSTRAINT PK__Message2 PRIMARY KEY CLUSTERED 
		(PivotId ASC, FactId ASC, AncestorFactId ASC, AncestorRoleId ASC)
)

INSERT INTO Message2
	(PivotId, FactId, ClientId, AncestorFactId, AncestorRoleId)
SELECT PivotId, FactId, ClientId, AncestorFactId, AncestorRoleId
FROM [Message]

DROP INDEX IX_Message_Ancestor ON [Message]

DROP TABLE [Message]

EXEC sp_rename 'Message2', 'Message'

CREATE NONCLUSTERED INDEX IX_Message_Ancestor ON [Message]
	(AncestorFactId ASC, AncestorRoleId ASC)

CREATE NONCLUSTERED INDEX IX_Message_Fact ON [Message]
    (FactId ASC)
