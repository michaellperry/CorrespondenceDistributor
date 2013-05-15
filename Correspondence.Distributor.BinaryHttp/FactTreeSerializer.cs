using System;
using System.IO;
using System.Linq;
using UpdateControls.Correspondence.Mementos;
using UpdateControls.Correspondence.FieldSerializer;
using System.Collections.Generic;

namespace UpdateControls.Correspondence.BinaryHTTPClient
{
    public class FactTreeSerializer
    {
        private const int MaxFactTypeCount = 1000;
        private const int MaxRoleCount = 1000;
        private const int MaxFactCount = 1000;
        private const int MaxDataLength = 1024;
        private const int MaxPredecessorCount = 100;

        private List<CorrespondenceFactType> _factTypes = new List<CorrespondenceFactType>();
        private List<RoleMemento> _roles = new List<RoleMemento>();

        public void SerlializeFactTree(FactTreeMemento factTreeMemento, BinaryWriter factWriter)
        {
            CollectFactTypesAndRoles(factTreeMemento);

            BinaryHelper.WriteLong(factTreeMemento.DatabaseId, factWriter);

            BinaryHelper.WriteShort((short)_factTypes.Count, factWriter);
            foreach (var factType in _factTypes)
            {
                BinaryHelper.WriteString(factType.TypeName, factWriter);
                BinaryHelper.WriteInt(factType.Version, factWriter);
            }

            BinaryHelper.WriteShort((short)_roles.Count, factWriter);
            foreach (var role in _roles)
            {
                BinaryHelper.WriteShort(GetFactTypeId(role.DeclaringType), factWriter);
                BinaryHelper.WriteString(role.RoleName, factWriter);
            }

            short factCount = (short)factTreeMemento.Facts.Count();
            BinaryHelper.WriteShort(factCount, factWriter);
            foreach (var fact in factTreeMemento.Facts)
            {
                if (fact is IdentifiedFactMemento)
                    SerializeIdentifiedFactMemento((IdentifiedFactMemento)fact, factWriter);
                else
                    SerializeIdentifiedFactRemote((IdentifiedFactRemote)fact, factWriter);
            }
        }

        private void CollectFactTypesAndRoles(FactTreeMemento factTreeMemento)
        {
            foreach (var fact in factTreeMemento.Facts)
            {
                if (fact is IdentifiedFactMemento)
                {
                    AddFactType(((IdentifiedFactMemento)fact).Memento.FactType);
                    foreach (var predecessor in ((IdentifiedFactMemento)fact).Memento.Predecessors)
                    {
                        AddFactType(predecessor.Role.DeclaringType);
                        AddRole(predecessor.Role);
                    }
                }
            }
        }

        public void AddFactType(CorrespondenceFactType factType)
        {
            if (!_factTypes.Contains(factType))
                _factTypes.Add(factType);
        }

        public void AddRole(RoleMemento role)
        {
            if (!_roles.Contains(role))
                _roles.Add(role);
        }

        public short GetFactTypeId(CorrespondenceFactType factType)
        {
            return (short)_factTypes.IndexOf(factType);
        }

        public short GetRoleId(RoleMemento role)
        {
            return (short)_roles.IndexOf(role);
        }

        public CorrespondenceFactType GetFactType(short factTypeId)
        {
            if (0 > factTypeId || factTypeId >= _factTypes.Count)
                throw new CorrespondenceException(String.Format("Fact type id {0} is out of range.", factTypeId));
            return _factTypes[factTypeId];
        }

        public RoleMemento GetRole(short roleId)
        {
            if (0 > roleId || roleId >= _roles.Count)
                throw new CorrespondenceException(String.Format("Role id {0} is out of range.", roleId));
            return _roles[roleId];
        }

        private void SerializeIdentifiedFactMemento(IdentifiedFactMemento factMemento, BinaryWriter factWriter)
        {
            BinaryHelper.WriteLong(factMemento.Id.key, factWriter);
            BinaryHelper.WriteBoolean(true, factWriter);
            BinaryHelper.WriteShort(GetFactTypeId(factMemento.Memento.FactType), factWriter);
            short dataSize = factMemento.Memento.Data == null ? (short)0 : (short)(factMemento.Memento.Data.Length);
            BinaryHelper.WriteShort(dataSize, factWriter);
            if (dataSize != 0)
                factWriter.Write(factMemento.Memento.Data);

            short predecessorsCount = (short)factMemento.Memento.Predecessors.Count();
            BinaryHelper.WriteShort(predecessorsCount, factWriter);
            foreach (var predecessor in factMemento.Memento.Predecessors)
            {
                BinaryHelper.WriteShort(GetRoleId(predecessor.Role), factWriter);
                BinaryHelper.WriteBoolean(predecessor.IsPivot, factWriter);
                BinaryHelper.WriteLong(predecessor.ID.key, factWriter);
            }
        }

        private void SerializeIdentifiedFactRemote(IdentifiedFactRemote identifiedFactRemote, BinaryWriter factWriter)
        {
            BinaryHelper.WriteLong(identifiedFactRemote.Id.key, factWriter);
            BinaryHelper.WriteBoolean(false, factWriter);
            BinaryHelper.WriteLong(identifiedFactRemote.RemoteId.key, factWriter);
        }

        public FactTreeMemento DeserializeFactTree(BinaryReader factReader)
        {
            long databaseId = BinaryHelper.ReadLong(factReader);
            FactTreeMemento factTreeMemento = new FactTreeMemento(databaseId);

            short factTypeCount = BinaryHelper.ReadShort(factReader);
            if (factTypeCount > MaxFactTypeCount)
                throw new CorrespondenceException("Maximum number of fact types exceeded.");
            for (short i = 0; i < factTypeCount; i++)
            {
                string typeName = BinaryHelper.ReadString(factReader);
                int version = BinaryHelper.ReadInt(factReader);
                _factTypes.Add(new CorrespondenceFactType(typeName, version));
            }

            short roleCount = BinaryHelper.ReadShort(factReader);
            if (roleCount > MaxRoleCount)
                throw new CorrespondenceException("Maximum number of roles exceeded.");
            for (short i = 0; i < roleCount; i++)
            {
                short factTypeId = BinaryHelper.ReadShort(factReader);
                string roleName = BinaryHelper.ReadString(factReader);
                _roles.Add(new RoleMemento(GetFactType(factTypeId), roleName, null, false));
            }

            short factCount = BinaryHelper.ReadShort(factReader);
            if (factCount > MaxFactCount)
                throw new CorrespondenceException("Maximum number of facts exceeded.");
            for (short i = 0; i < factCount; i++)
            {
                factTreeMemento.Add(DeserlializeFact(factReader));
            }
            return factTreeMemento;
        }

        private IdentifiedFactBase DeserlializeFact(BinaryReader factReader)
        {
            long factId;
            bool isMemento;

            factId = BinaryHelper.ReadLong(factReader);
            isMemento = BinaryHelper.ReadBoolean(factReader);
            if (isMemento)
                return DeserlializeIdentifiedFactMemento(factReader, factId);
            else
                return DeserializeIdentifiedFactRemote(factReader, factId);
        }

        private IdentifiedFactMemento DeserlializeIdentifiedFactMemento(BinaryReader factReader, long factId)
        {
            short dataSize;
            byte[] data;
            short predecessorCount;

            CorrespondenceFactType factType = GetFactType(BinaryHelper.ReadShort(factReader));
            dataSize = BinaryHelper.ReadShort(factReader);
            if (dataSize > MaxDataLength)
                throw new CorrespondenceException("Maximum data length exceeded.");
            data = dataSize > 0 ? factReader.ReadBytes(dataSize) : new byte[0];
            predecessorCount = BinaryHelper.ReadShort(factReader);
            if (predecessorCount > MaxPredecessorCount)
                throw new CorrespondenceException("Maximum predecessor count exceeded.");

            FactMemento factMemento = new FactMemento(factType);
            factMemento.Data = data;
            for (short i = 0; i < predecessorCount; i++)
            {
                bool isPivot;
                long predecessorFactId;

                RoleMemento role = GetRole(BinaryHelper.ReadShort(factReader));
                isPivot = BinaryHelper.ReadBoolean(factReader);
                predecessorFactId = BinaryHelper.ReadLong(factReader);

                factMemento.AddPredecessor(
                    role,
                    new FactID() { key = predecessorFactId },
                    isPivot
                );
            }

            return new IdentifiedFactMemento(new FactID { key = factId }, factMemento);
        }

        private IdentifiedFactRemote DeserializeIdentifiedFactRemote(BinaryReader factReader, long factId)
        {
            long remoteFactId;

            remoteFactId = BinaryHelper.ReadLong(factReader);
            return new IdentifiedFactRemote(new FactID { key = factId }, new FactID { key = remoteFactId });
        }
    }
}
