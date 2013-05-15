using System;
using System.Collections.Generic;
using System.IO;
using UpdateControls.Correspondence.FieldSerializer;
using UpdateControls.Correspondence.Mementos;

namespace UpdateControls.Correspondence.BinaryHTTPClient
{
    public abstract class BinaryResponse
    {
        public static byte Version = 1;

        public void Write(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Version, requestWriter);
            WriteInternal(requestWriter);
        }

        protected abstract void WriteInternal(BinaryWriter requestWriter);
    }
    public class GetManyResponse : BinaryResponse
    {
        public static byte Token = 1;

        public FactTreeMemento FactTree { get; set; }
        public List<FactTimestamp> PivotIds { get; set; }

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
            new FactTreeSerializer().SerlializeFactTree(FactTree, requestWriter);
            BinaryHelper.WriteShort((short)PivotIds.Count, requestWriter);
            foreach (var pivotId in PivotIds)
            {
                BinaryHelper.WriteLong(pivotId.FactId, requestWriter);
                BinaryHelper.WriteLong(pivotId.TimestampId, requestWriter);
            }
        }
    }
    public class PostResponse : BinaryResponse
    {
        public static byte Token = 2;

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
        }
    }
    public class InterruptResponse : BinaryResponse
    {
        public static byte Token = 5;

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
        }
    }
    public class NotifyResponse : BinaryResponse
    {
        public static byte Token = 6;

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
        }
    }
    public class WindowsSubscribeResponse : BinaryResponse
    {
        public static byte Token = 7;

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
        }
    }
    public class WindowsUnsubscribeResponse : BinaryResponse
    {
        public static byte Token = 8;

        protected override void WriteInternal(BinaryWriter requestWriter)
        {
            BinaryHelper.WriteByte(Token, requestWriter);
        }
    }
}
