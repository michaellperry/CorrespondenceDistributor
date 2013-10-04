ALTER TABLE [Message]
	DROP CONSTRAINT PK__Message

ALTER TABLE [Message]
    ADD CONSTRAINT PK__Message PRIMARY KEY CLUSTERED
	    (PivotId, FactId, AncestorFactId, AncestorRoleId)

CREATE NONCLUSTERED INDEX IX_Message_Fact ON [Message]
    (FactId ASC)
