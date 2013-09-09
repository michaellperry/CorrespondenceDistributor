CREATE TABLE WindowsPhoneSubscription
(
    PivotFactID bigint NOT NULL,
    DeviceUri varchar(1024) NOT NULL,
    ClientID int NOT NULL,
  
    CONSTRAINT PK_WindowsPhoneSubscription PRIMARY KEY (PivotFactID, DeviceUri)
);
