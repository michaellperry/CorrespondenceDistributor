using System;
using System.Collections.Generic;
using System.IO;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.FieldSerializer;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.BinaryHttp
{
    public abstract class BinaryRequest
    {
        public static byte Version = 2;

        public static BinaryRequest Read(BinaryReader requestReader)
        {
            byte version = BinaryHelper.ReadByte(requestReader);
            if (version != BinaryRequest.Version)
                throw new CorrespondenceException(String.Format("This distributor cannot read version {0} requests.", version));

            string domain = BinaryHelper.ReadString(requestReader);

            byte token = BinaryHelper.ReadByte(requestReader);
            if (token == GetManyRequest.Token)
                return GetManyRequest.CreateAndRead(domain, requestReader);
            else if (token == PostResponse.Token)
                return PostRequest.CreateAndRead(domain, requestReader);
            else if (token == WindowsPhoneSubscribeRequest.Token)
                return WindowsPhoneSubscribeRequest.CreateAndRead(domain, requestReader);
            else if (token == WindowsPhoneUnsubscribeRequest.Token)
                return WindowsPhoneUnsubscribeRequest.CreateAndRead(domain, requestReader);
            else if (token == InterruptRequest.Token)
                return InterruptRequest.CreateAndRead(domain, requestReader);
            else if (token == NotifyRequest.Token)
                return NotifyRequest.CreateAndRead(domain, requestReader);
            else
                throw new CorrespondenceException(String.Format("Unknown token {0}.", token));
        }

        public string Domain { get; set; }
    }
    public class GetManyRequest : BinaryRequest
    {
        public static byte Token = 1;

        public FactTreeMemento PivotTree { get; set; }
        public List<FactTimestamp> PivotIds { get; set; }
        public Guid ClientGuid { get; set; }
        public int TimeoutSeconds { get; set; }

        public static GetManyRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            GetManyRequest request = new GetManyRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        private void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;
            PivotTree = new FactTreeSerializer().DeserializeFactTree(requestReader);
            short pivotCount = BinaryHelper.ReadShort(requestReader);
            if (pivotCount < 0 || pivotCount > 256)
                throw new CorrespondenceException(String.Format("Incorrect number of pivots in Get Many request: {0}.", pivotCount));

            PivotIds = new List<FactTimestamp>(pivotCount);
            for (short pivotIndex = 0; pivotIndex < pivotCount; ++pivotIndex)
            {
                long factId = BinaryHelper.ReadLong(requestReader);
                long timestampId = BinaryHelper.ReadLong(requestReader);
                PivotIds.Add(new FactTimestamp
                {
                    FactId      = factId,
                    TimestampId = timestampId
                });
            }
            ClientGuid = Guid.Parse(BinaryHelper.ReadString(requestReader));
            TimeoutSeconds = BinaryHelper.ReadInt(requestReader);
        }
    }
    public class PostRequest : BinaryRequest
    {
        public static byte Token = 2;

        public FactTreeMemento MessageBody { get; set; }
        public Guid ClientGuid { get; set; }
        public List<UnpublishMemento> UnpublishedMessages { get; set; }

        public static PostRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            var request = new PostRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        private void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;

            FactTreeSerializer factTreeSerlializer = new FactTreeSerializer();
            MessageBody = factTreeSerlializer.DeserializeFactTree(requestReader);

            ClientGuid = Guid.Parse(BinaryHelper.ReadString(requestReader));
            short unpublishedMessageCount = BinaryHelper.ReadShort(requestReader);
            if (unpublishedMessageCount < 0 || unpublishedMessageCount > 256)
                throw new CorrespondenceException(String.Format("Incorrect number of unpublished messages in Post request: {0}.", unpublishedMessageCount));

            UnpublishedMessages = new List<UnpublishMemento>(unpublishedMessageCount);
            for (short unpublishedMessageIndex = 0; unpublishedMessageIndex < unpublishedMessageCount; ++unpublishedMessageIndex)
            {
                var messageId = BinaryHelper.ReadLong(requestReader);
                var roleId = BinaryHelper.ReadShort(requestReader);
                UnpublishedMessages.Add(new UnpublishMemento(
                    new FactID { key = messageId },
                    factTreeSerlializer.GetRole(roleId)
                ));
            }
        }
    }
    public class WindowsPhoneSubscribeRequest : BinaryRequest
    {
        public static byte Token = 3;

        public FactTreeMemento PivotTree { get; set; }
        public long PivotId { get; set; }
        public string DeviceUri { get; set; }
        public Guid ClientGuid { get; set; }

        public static WindowsPhoneSubscribeRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            WindowsPhoneSubscribeRequest request = new WindowsPhoneSubscribeRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        public void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;
            PivotTree = new FactTreeSerializer().DeserializeFactTree(requestReader);
            PivotId = BinaryHelper.ReadLong(requestReader);
            DeviceUri = BinaryHelper.ReadString(requestReader);
            ClientGuid = Guid.Parse(BinaryHelper.ReadString(requestReader));
        }
    }
    public class WindowsPhoneUnsubscribeRequest : BinaryRequest
    {
        public static byte Token = 4;

        public FactTreeMemento PivotTree { get; set; }
        public long PivotId { get; set; }
        public string DeviceUri { get; set; }

        public static WindowsPhoneUnsubscribeRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            WindowsPhoneUnsubscribeRequest request = new WindowsPhoneUnsubscribeRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        public void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;
            PivotTree = new FactTreeSerializer().DeserializeFactTree(requestReader);
            PivotId = BinaryHelper.ReadLong(requestReader);
            DeviceUri = BinaryHelper.ReadString(requestReader);
        }
    }
    public class InterruptRequest : BinaryRequest
    {
        public static byte Token = 5;

        public Guid ClientGuid { get; set; }

        public static InterruptRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            InterruptRequest request = new InterruptRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        private void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;
            ClientGuid = Guid.Parse(BinaryHelper.ReadString(requestReader));
        }
    }
    public class NotifyRequest : BinaryRequest
    {
        public static byte Token = 6;

        public FactTreeMemento PivotTree { get; set; }
        public long PivotId { get; set; }
        public Guid ClientGuid { get; set; }
        public string Text1 { get; set; }
        public string Text2 { get; set; }

        public static NotifyRequest CreateAndRead(string domain, BinaryReader requestReader)
        {
            NotifyRequest request = new NotifyRequest();
            request.ReadInternal(domain, requestReader);
            return request;
        }

        public void ReadInternal(string domain, BinaryReader requestReader)
        {
            Domain = domain;
            PivotTree = new FactTreeSerializer().DeserializeFactTree(requestReader);
            PivotId = BinaryHelper.ReadLong(requestReader);
            ClientGuid = Guid.Parse(BinaryHelper.ReadString(requestReader));
            Text1 = BinaryHelper.ReadString(requestReader);
            Text2 = BinaryHelper.ReadString(requestReader);
        }
    }
}
