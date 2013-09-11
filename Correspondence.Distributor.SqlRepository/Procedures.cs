using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    class Procedures : IDisposable
    {
        private const string PRE_HEAD_SELECT =
            "SELECT ff.FactID, ff.Data, t.TypeName, t.Version, dt.TypeName, dt.Version, r.RoleName, p.FKPredecessorFactID, p.IsPivot " +
            "\r\nFROM (SELECT ";
        private const string POST_HEAD_SELECT =
            "f.FactID, f.Data, f.FKTypeID FROM Fact f ";
        private const string HEAD_SELECT = PRE_HEAD_SELECT + POST_HEAD_SELECT;
        private const string TAIL_JOIN =
            "\r\n) ff " +
            "\r\nJOIN Type t ON ff.FKTypeID = t.TypeID " +
            "\r\nLEFT JOIN Predecessor p ON ff.FactID = p.FKFactID " +
            "\r\nLEFT JOIN Role r ON p.FKRoleID = r.RoleID " +
            "\r\nLEFT JOIN Type dt ON r.DeclaringTypeID = dt.TypeID ";

        private readonly Session _session;

        public Procedures(Session session)
        {
            _session = session;
        }

        public void BeginTransaction()
        {
            _session.BeginTransaction();
        }

        public void Commit()
        {
            _session.Commit();
        }

        public FactID InsertFact(FactMemento fact, int typeId)
        {
            _session.Command.CommandText = "INSERT Fact (FKTypeID, Data, Hashcode) VALUES (@TypeID, @Data, @Hashcode)";
            AddParameter("@TypeID", typeId);
            AddParameter("@Data", fact.Data);
            AddParameter("@Hashcode", fact.GetHashCode());
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();

            _session.Command.CommandText = "SELECT @@IDENTITY";
            decimal result = (decimal)_session.Command.ExecuteScalar();
            _session.Command.Parameters.Clear();
            return new FactID { key = (Int64)result };
        }

        public List<AncestorPivot> GetAncestorPivots(string[] nonPivots)
        {
            string nonPivotGroup = string.Join(",", nonPivots);
            _session.Command.CommandText = string.Format(
                "SELECT DISTINCT AncestorFactId, AncestorRoleId, PivotId FROM Message " +
                    "WHERE FactId IN ({0})",
                nonPivotGroup);
            using (var loader = new Loader(_session.Command.ExecuteReader()))
            {
                _session.Command.Parameters.Clear();
                return loader.LoadAncestorPivots().ToList();
            }
        }

        public void InsertPredecessor(FactID id, PredecessorMemento predecessor, int roleId)
        {
            _session.Command.CommandText = "INSERT Predecessor (FKFactID, FKRoleID, FKPredecessorFactID, IsPivot) VALUES (@FactID, @RoleID, @PredecessorFactID, @IsPivot)";
            AddParameter("@FactID", id.key);
            AddParameter("@RoleID", roleId);
            AddParameter("@PredecessorFactID", predecessor.ID.key);
            AddParameter("@IsPivot", predecessor.IsPivot);
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();
        }

        public List<FactID> GetRecentMessages(FactID pivotId, TimestampID timestamp, int clientId)
        {
            _session.Command.CommandText =
                "SELECT TOP (20) FactId " +
                "FROM Message " +
                "WHERE PivotId = @PivotId " +
                "AND FactId > @Timestamp " +
                "AND ClientId != @ClientId " +
                "ORDER BY FactId";
            AddParameter("@PivotId", pivotId.key);
            AddParameter("@Timestamp", timestamp.Key);
            AddParameter("@ClientId", clientId);
            using (var loader = new Loader(_session.Command.ExecuteReader()))
            {
                _session.Command.Parameters.Clear();

                return loader.LoadIDs().ToList();
            }
        }

        public List<IdentifiedFactMemento> GetEqualFactsByHashCode(FactMemento memento, bool readCommitted, int typeId)
        {
            _session.Command.CommandText = HEAD_SELECT +
                (readCommitted ? "" : "WITH (NOLOCK) ") +
                "WHERE f.FKTypeID = @TypeID AND f.Hashcode = @Hashcode " +
                TAIL_JOIN +
                "ORDER BY ff.FactID, p.PredecessorID";
            AddParameter("@TypeID", typeId);
            AddParameter("@Hashcode", memento.GetHashCode());
            using (var loader = new Loader(_session.Command.ExecuteReader()))
            {
                _session.Command.Parameters.Clear();

                return loader.LoadMementos()
                    .Where(im => im.Memento.Equals(memento))
                    .ToList();
            }
        }

        public void InsertMessages(IEnumerable<AncestorMessage> roleMessages, int clientId)
        {
            _session.Command.CommandText = "INSERT INTO Message (FactId, PivotId, ClientId, AncestorFactId, AncestorRoleId) " +
                "VALUES (@FactId, @PivotId, @ClientId, @AncestorFactId, @AncestorRoleId)";
            foreach (AncestorMessage roleMessage in roleMessages)
            {
                AddParameter("@FactId", roleMessage.Message.FactId.key);
                AddParameter("@PivotId", roleMessage.Message.PivotId.key);
                AddParameter("@ClientId", clientId);
                AddParameter("@AncestorFactId", roleMessage.AncestorFactId.key);
                AddParameter("@AncestorRoleId", roleMessage.AncestorRoleId);
                _session.Command.ExecuteNonQuery();
                _session.Command.Parameters.Clear();
            }
        }

        public void DeleteMessage(FactID ancestorFactId, int ancestorRoleId)
        {
            _session.Command.CommandText = "DELETE FROM Message " +
                "WHERE AncestorFactId = @AncestorFactId AND AncestorRoleId = @AncestorRoleId";
            AddParameter("@AncestorFactId", ancestorFactId.key);
            AddParameter("@AncestorRoleId", ancestorRoleId);
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();
        }

        public int GetRoleId(RoleMemento roleMemento, int declaringTypeId)
        {
            int roleId = 0;
            _session.Command.CommandText = "SELECT RoleID FROM Role WHERE RoleName = @RoleName AND DeclaringTypeID = @DeclaringTypeID";
            AddParameter("@RoleName", roleMemento.RoleName);
            AddParameter("@DeclaringTypeID", declaringTypeId);
            using (IDataReader typeReader = _session.Command.ExecuteReader())
            {
                _session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    roleId = typeReader.GetInt32(0);
                }
            }
            return roleId;
        }

        public int InsertRole(RoleMemento roleMemento, int declaringTypeId)
        {
            int roleId;
            _session.Command.CommandText = "INSERT INTO Role (RoleName, DeclaringTypeID) VALUES (@RoleName, @DeclaringTypeID)";
            AddParameter("@RoleName", roleMemento.RoleName);
            AddParameter("@DeclaringTypeID", declaringTypeId);
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();

            _session.Command.CommandText = "SELECT @@IDENTITY";
            roleId = (int)(decimal)_session.Command.ExecuteScalar();
            _session.Command.Parameters.Clear();
            return roleId;
        }

        public int GetTypeId(CorrespondenceFactType typeMemento)
        {
            int typeId = 0;
            _session.Command.CommandText = "SELECT TypeID FROM Type WHERE TypeName = @TypeName AND Version = @Version";
            AddParameter("@TypeName", typeMemento.TypeName);
            AddParameter("@Version", typeMemento.Version);
            using (IDataReader typeReader = _session.Command.ExecuteReader())
            {
                _session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    typeId = typeReader.GetInt32(0);
                }
            }
            return typeId;
        }

        public int InsertType(CorrespondenceFactType typeMemento)
        {
            int typeId;
            _session.Command.CommandText = "INSERT INTO Type (TypeName, Version) VALUES (@TypeName, @Version)";
            AddParameter("@TypeName", typeMemento.TypeName);
            AddParameter("@Version", typeMemento.Version);
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();

            _session.Command.CommandText = "SELECT @@IDENTITY";
            typeId = (int)(decimal)_session.Command.ExecuteScalar();
            _session.Command.Parameters.Clear();
            return typeId;
        }

        public IdentifiedFactMemento GetIdentifiedMemento(FactID factId)
        {
            // Get the fact.
            _session.Command.CommandText = HEAD_SELECT +
                "WHERE f.FactID = @FactID " +
                TAIL_JOIN +
                "ORDER BY p.PredecessorID";
            AddParameter("@FactID", factId.key);
            using (var loader = new Loader(_session.Command.ExecuteReader()))
            {
                _session.Command.Parameters.Clear();
                return loader.LoadMementos().FirstOrDefault();
            }
        }

        public int GetClientId(Guid clientGuid)
        {
            int clientId = 0;
            _session.Command.CommandText = "SELECT ClientId FROM Client WHERE ClientGuid = @ClientGuid";
            AddParameter("@ClientGuid", clientGuid);
            using (IDataReader typeReader = _session.Command.ExecuteReader())
            {
                _session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    clientId = typeReader.GetInt32(0);
                }
            }
            return clientId;
        }

        public int InsertClient(Guid clientGuid)
        {
            int clientId;
            _session.Command.CommandText = "INSERT INTO Client (ClientGuid) VALUES (@ClientGuid)";
            AddParameter("@ClientGuid", clientGuid);
            _session.Command.ExecuteNonQuery();
            _session.Command.Parameters.Clear();

            _session.Command.CommandText = "SELECT @@IDENTITY";
            clientId = (int)(decimal)_session.Command.ExecuteScalar();
            _session.Command.Parameters.Clear();
            return clientId;
        }

        public void InsertWindowsPhoneSubscriptions(
            IEnumerable<FactID> pivotIds, 
            string deviceUri, 
            int clientId)
        {
            _session.Command.CommandText =
                "MERGE WindowsPhoneSubscription s " +
                "USING (SELECT @PivotFactID as PivotFactID, @DeviceUri as DeviceUri) as n " +
                "ON n.PivotFactID = s.PivotFactID AND n.DeviceUri = s.DeviceUri " +
                "WHEN NOT MATCHED THEN " +
                "INSERT (PivotFactID, DeviceUri, ClientID) " +
                "VALUES (n.PivotFactID, n.DeviceUri, @ClientID);";
            foreach (var pivotId in pivotIds)
            {
                AddParameter("@PivotFactId", pivotId.key);
                AddParameter("@DeviceUri", deviceUri);
                AddParameter("@ClientId", clientId);
                _session.Command.ExecuteNonQuery();
                _session.Command.Parameters.Clear();
            }
        }

        public List<WindowsPhoneSubscription> GetWindowsPhoneSubscriptionsByPivot(
            IEnumerable<FactID> pivotIds, int clientId)
        {
            string pivotIdGroup = string.Join(",", pivotIds
                .Select(id => id.key.ToString())
                .ToArray());
            _session.Command.CommandText = string.Format(
                "SELECT PivotFactId, DeviceUri " +
                "FROM WindowsPhoneSubscription " +
                "WHERE PivotFactId IN ({0}) " +
                "AND ClientId != @ClientId",
                pivotIdGroup);
            AddParameter("@ClientId", clientId);
            using (var loader = new Loader(_session.Command.ExecuteReader()))
            {
                return loader.LoadWindowsPhoneSubscriptions().ToList();
            }
        }

        public void DeleteWindowsPhoneSubscriptions(
            IEnumerable<FactID> pivotIds, string deviceUri)
        {
            string pivotIdGroup = string.Join(",", pivotIds
                .Select(id => id.key.ToString())
                .ToArray());
            _session.Command.CommandText = string.Format(
                "DELETE WindowsPhoneSubscription " +
                "WHERE PivotFactId IN ({0}) " +
                "AND DeviceUri = @DeviceUri",
                pivotIdGroup);
            AddParameter("@DeviceUri", deviceUri);
            _session.Command.ExecuteNonQuery();
        }

        public void DeleteWindowsPhoneSubscriptionsByDeviceId(IEnumerable<string> deviceUris)
        {
            string deviceUriGroup = string.Join(",", deviceUris
                .Select(deviceUri => deviceUri)
                .ToArray());
            _session.Command.CommandText = string.Format(
                "DELETE WindowsPhoneSubscription " +
                "WHERE DeviceUri IN ({0})",
                deviceUriGroup);
            _session.Command.ExecuteNonQuery();
        }

        private void AddParameter(string name, object value)
        {
            var param = _session.Command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            _session.Command.Parameters.Add(param);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (_session != null)
                    _session.Dispose();
        }

        ~Procedures()
        {
            Dispose(false);
        }
    }
}
