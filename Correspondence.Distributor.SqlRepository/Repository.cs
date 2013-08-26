using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Reflection;
using UpdateControls.Correspondence;

namespace Correspondence.Distributor.SqlRepository
{
    public class Repository : IRepository
    {
        private const string PRE_HEAD_SELECT =
            "SELECT ff.FactID, ff.Data, t.TypeID, t.TypeName, t.Version, r.RoleID, dt.TypeID, dt.TypeName, dt.Version, r.RoleName, p.FKPredecessorFactID, p.IsPivot " +
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

        private readonly string _connectionString;

        public event Delegates.PivotAffectedDelegate PivotAffected;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public FactMemento Load(string domain, FactID factId)
        {
            throw new NotImplementedException();
        }

        public FactID Save(string domain, FactMemento fact, string clientGuid)
        {
            using (var session = new Session(_connectionString))
            {
                // First see if the fact is already in storage.
                FactID id;
                if (FindExistingFact(fact, out id, session))
                    return id;

                // It isn't there, so store it.
                int typeId = SaveType(session, fact.FactType);
                session.Command.CommandText = "INSERT Fact (FKTypeID, Data, Hashcode) VALUES (@TypeID, @Data, @Hashcode)";
                AddParameter(session.Command, "@TypeID", typeId);
                AddParameter(session.Command, "@Data", fact.Data);
                AddParameter(session.Command, "@Hashcode", fact.GetHashCode());
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();

                session.Command.CommandText = "SELECT @@IDENTITY";
                decimal result = (decimal)session.Command.ExecuteScalar();
                session.Command.Parameters.Clear();
                id.key = (Int64)result;

                // Store the predecessors.
                foreach (PredecessorMemento predecessor in fact.Predecessors)
                {
                    int roleId = SaveRole(session, predecessor.Role);
                    session.Command.CommandText = "INSERT Predecessor (FKFactID, FKRoleID, FKPredecessorFactID, IsPivot) VALUES (@FactID, @RoleID, @PredecessorFactID, @IsPivot)";
                    AddParameter(session.Command, "@FactID", id.key);
                    AddParameter(session.Command, "@RoleID", roleId);
                    AddParameter(session.Command, "@PredecessorFactID", predecessor.ID.key);
                    AddParameter(session.Command, "@IsPivot", predecessor.IsPivot);
                    session.Command.ExecuteNonQuery();
                    session.Command.Parameters.Clear();
                }

                // Store a message for each pivot.
                FactID newFactId = id;
                List<MessageMemento> pivotMessages = fact.Predecessors
                    .Where(predecessor => predecessor.IsPivot)
                    .Select(predecessor => new MessageMemento(predecessor.ID, newFactId))
                    .ToList();

                // Store messages for each non-pivot. This fact belongs to all predecessors' pivots.
                string[] nonPivots = fact.Predecessors
                    .Where(predecessor => !predecessor.IsPivot)
                    .Select(predecessor => predecessor.ID.key.ToString())
                    .ToArray();
                List<MessageMemento> nonPivotMessages;
                if (nonPivots.Length > 0)
                {
                    string nonPivotGroup = string.Join(",", nonPivots);
                    session.Command.CommandText = string.Format(
                        "SELECT DISTINCT PivotId FROM Message WHERE FactId IN ({0})",
                        nonPivotGroup);
                    List<FactID> predecessorsPivots;
                    using (IDataReader predecessorPivotReader = session.Command.ExecuteReader())
                    {
                        session.Command.Parameters.Clear();
                        predecessorsPivots = LoadIDsFromReader(predecessorPivotReader).ToList();
                    }

                    nonPivotMessages = predecessorsPivots
                        .Select(predecessorPivot => new MessageMemento(predecessorPivot, newFactId))
                        .ToList();
                }
                else
                    nonPivotMessages = new List<MessageMemento>();

                int peerId = 0;
                SaveMessages(session, pivotMessages.Union(nonPivotMessages).Distinct(), peerId);

                return id;
            }
        }

        public FactID? FindExistingFact(string domain, FactMemento fact)
        {
            throw new NotImplementedException();
        }

        public List<FactID> LoadRecentMessages(string domain, FactID pivotId, string clientGuid, TimestampID timestamp)
        {
            throw new NotImplementedException();
        }

        private bool FindExistingFact(FactMemento memento, out FactID id, Session session)
        {
            int typeId = SaveType(session, memento.FactType);

            // Load all candidates that have the same hash code.
            session.Command.CommandText = HEAD_SELECT +
                "WHERE f.FKTypeID = @TypeID AND f.Hashcode = @Hashcode " +
                TAIL_JOIN +
                "ORDER BY ff.FactID, p.PredecessorID";
            AddParameter(session.Command, "@TypeID", typeId);
            AddParameter(session.Command, "@Hashcode", memento.GetHashCode());
            using (IDataReader factReader = session.Command.ExecuteReader())
            {
                session.Command.Parameters.Clear();

                List<IdentifiedFactMemento> existingFact = LoadMementosFromReader(factReader).Where(im => im.Memento.Equals(memento)).ToList();
                if (existingFact.Count > 1)
                    throw new CorrespondenceException(string.Format("More than one fact matched the given {0}.", memento.FactType));
                if (existingFact.Count == 1)
                {
                    id = existingFact[0].Id;
                    return true;
                }
                else
                {
                    id = new FactID();
                    return false;
                }
            }
        }

        private void SaveMessages(Session session, IEnumerable<MessageMemento> messages, int peerId)
        {
            session.Command.CommandText = "INSERT INTO Message (FactId, PivotId, PeerId) VALUES (@FactId, @PivotId, @PeerId)";
            foreach (MessageMemento message in messages)
            {
                AddParameter(session.Command, "@FactId", message.FactId.key);
                AddParameter(session.Command, "@PivotId", message.PivotId.key);
                AddParameter(session.Command, "@PeerId", peerId);
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();
            }
        }

        private IEnumerable<IdentifiedFactMemento> LoadMementosFromReader(IDataReader factReader)
        {
            IdentifiedFactMemento current = null;

            while (factReader.Read())
            {
                // FactID, Data, TypeID, TypeName, Version, RoleID, DeclaringTypeID, DeclaringTypeName, DeclaringTypeVersion, RoleName, PredecessorFactID
                long factId = factReader.GetInt64(0);

                // Load the header.
                if (current == null || factId != current.Id.key)
                {
                    if (current != null)
                        yield return current;

                    int typeId = factReader.GetInt32(2);
                    string typeName = factReader.GetString(3);
                    int typeVersion = factReader.GetInt32(4);

                    // Create the memento.
                    current = new IdentifiedFactMemento(
                        new FactID() { key = factId },
                        new FactMemento(GetTypeMemento(typeId, typeName, typeVersion)));
                    ReadBinary(factReader, current.Memento, 1);
                }

                // Load a predecessor.
                if (!factReader.IsDBNull(5))
                {
                    int roleId = factReader.GetInt32(5);
                    int declaringTypeId = factReader.GetInt32(6);
                    string declaringTypeName = factReader.GetString(7);
                    int declaringTypeVersion = factReader.GetInt32(8);
                    string roleName = factReader.GetString(9);
                    long predecessorFactId = factReader.GetInt64(10);
                    bool isPivot = factReader.GetBoolean(11);

                    current.Memento.AddPredecessor(
                        GetRoleMemento(roleId, declaringTypeId, declaringTypeName, declaringTypeVersion, roleName),
                        new FactID() { key = predecessorFactId },
                        isPivot);
                }
            }

            if (current != null)
                yield return current;
        }

        private IEnumerable<FactID> LoadIDsFromReader(IDataReader factReader)
        {
            while (factReader.Read())
                yield return new FactID() { key = factReader.GetInt64(0) };
        }

        internal int SaveRole(Session session, RoleMemento roleMemento)
        {
            int declaringTypeId = SaveType(session, roleMemento.DeclaringType);

            // See if the role already exists.
            int roleId = 0;
            session.Command.CommandText = "SELECT RoleID FROM Role WHERE RoleName = @RoleName AND DeclaringTypeID = @DeclaringTypeID";
            AddParameter(session.Command, "@RoleName", roleMemento.RoleName);
            AddParameter(session.Command, "@DeclaringTypeID", declaringTypeId);
            using (IDataReader typeReader = session.Command.ExecuteReader())
            {
                session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    roleId = typeReader.GetInt32(0);
                }
            }

            // If not, create it.
            if (roleId == 0)
            {
                session.Command.CommandText = "INSERT INTO Role (RoleName, DeclaringTypeID) VALUES (@RoleName, @DeclaringTypeID)";
                AddParameter(session.Command, "@RoleName", roleMemento.RoleName);
                AddParameter(session.Command, "@DeclaringTypeID", declaringTypeId);
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();

                session.Command.CommandText = "SELECT @@IDENTITY";
                roleId = (int)(decimal)session.Command.ExecuteScalar();
                session.Command.Parameters.Clear();
            }

            return roleId;
        }

        private int SaveType(Session session, CorrespondenceFactType typeMemento)
        {
            // See if the type already exists.
            int typeId = 0;
            session.Command.CommandText = "SELECT TypeID FROM Type WHERE TypeName = @TypeName AND Version = @Version";
            AddParameter(session.Command, "@TypeName", typeMemento.TypeName);
            AddParameter(session.Command, "@Version", typeMemento.Version);
            using (IDataReader typeReader = session.Command.ExecuteReader())
            {
                session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    typeId = typeReader.GetInt32(0);
                }
            }

            // If not, create it.
            if (typeId == 0)
            {
                session.Command.CommandText = "INSERT INTO Type (TypeName, Version) VALUES (@TypeName, @Version)";
                AddParameter(session.Command, "@TypeName", typeMemento.TypeName);
                AddParameter(session.Command, "@Version", typeMemento.Version);
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();

                session.Command.CommandText = "SELECT @@IDENTITY";
                typeId = (int)(decimal)session.Command.ExecuteScalar();
                session.Command.Parameters.Clear();
            }

            return typeId;
        }

        private static void ReadBinary(IDataReader reader, FactMemento memento, int columnIndex)
        {
            byte[] buffer = new byte[1024];
            int length = (int)reader.GetBytes(columnIndex, 0, buffer, 0, buffer.Length);
            memento.Data = new byte[length];
            Array.Copy(buffer, memento.Data, length);
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }

        private CorrespondenceFactType GetTypeMemento(int typeId, string typeName, int typeVersion)
        {
            return new CorrespondenceFactType(typeName, typeVersion);
        }

        private RoleMemento GetRoleMemento(int roleId, int declaringTypeId, string declaringTypeTypeName, int declaringTypeVersion, string roleName)
        {
            return new RoleMemento(
                new CorrespondenceFactType(
                    declaringTypeTypeName,
                    declaringTypeVersion),
                roleName,
                null,
                false);
        }

        public Repository UpgradeDatabase()
        {
            using (var session = new Session(_connectionString))
            {
                session.Command.CommandType = CommandType.Text;
                ExecuteScript(0, session.Command);
                session.Command.CommandText = "SELECT Version FROM Correspondence_Version";
                int versionId = (int)session.Command.ExecuteScalar() + 1;
                while (ExecuteScript(versionId, session.Command))
                {
                    session.Command.CommandText = String.Format("UPDATE Correspondence_Version SET Version = {0}", versionId);
                    session.Command.ExecuteNonQuery();
                    versionId = versionId + 1;
                }
            }
            return this;
        }

        private static bool ExecuteScript(int versionId, IDbCommand command)
        {
            string scriptName = String.Format("Correspondence.{0}.sql", versionId);
            Stream scriptStream = GetScriptStream(scriptName);
            if (scriptStream == null)
                return false;

            command.CommandText = ReadScriptFromStream(scriptStream);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new ApplicationException(String.Format("Exception wile running scipt {0}. {1}", scriptName, ex.Message));
            }
            return true;
        }

        private static Stream GetScriptStream(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Repository), String.Format(@"Scripts.{0}", name));
        }

        private static string ReadScriptFromStream(Stream scriptStream)
        {
            using (StreamReader reader = new StreamReader(scriptStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
