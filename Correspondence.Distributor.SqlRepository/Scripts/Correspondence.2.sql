SELECT * from [Message];

ALTER TABLE [Message]
    ADD AncestorFactId bigint NOT NULL DEFAULT 0;

ALTER TABLE [Message]
    ADD AncestorRoleId int NOT NULL DEFAULT 0;

CREATE NONCLUSTERED INDEX [IX_Message_Ancestor] ON [Message]
(
	AncestorFactId ASC,
	AncestorRoleId ASC
)
