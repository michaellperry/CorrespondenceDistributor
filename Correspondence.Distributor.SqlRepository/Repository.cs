using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    public class Repository : IRepository
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

        private readonly string _connectionString;
        private bool _retried;

        public event Delegates.PivotAffectedDelegate PivotAffected;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool Retried
        {
            get { return _retried; }
            set { _retried = value; }
        }

        public FactMemento Load(string domain, FactID factId)
        {
            IdentifiedFactMemento identifiedMemento = null;

            using (var session = new Session(_connectionString))
            {
                // Get the fact.
                session.Command.CommandText = HEAD_SELECT +
                    "WHERE f.FactID = @FactID " +
                    TAIL_JOIN +
                    "ORDER BY p.PredecessorID";
                AddParameter(session.Command, "@FactID", factId.key);
                using (IDataReader reader = session.Command.ExecuteReader())
                {
                    session.Command.Parameters.Clear();
                    identifiedMemento = LoadMementosFromReader(reader).FirstOrDefault();
                    if (identifiedMemento == null)
                        throw new CorrespondenceException(string.Format("Unable to find fact {0}", factId.key));
                }
            }

            return identifiedMemento.Memento;
        }

        public FactID Save(string domain, FactMemento fact, Guid clientGuid)
        {
            // Retry on concurrency failure.
            while (true)
            {
                using (var session = new Session(_connectionString))
                {
                    session.BeginTransaction();

                    // First see if the fact is already in storage.
                    FactID id;
                    if (FindExistingFact(fact, out id, session, readCommitted: true))
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

                    int clientId = SaveClient(session, clientGuid);
                    var messages = pivotMessages.Union(nonPivotMessages).Distinct().ToList();
                    SaveMessages(session, messages, clientId);

                    // Optimistic concurrency check.
                    // Make sure we don't find more than one.
                    var existingFacts = FindExistingFacts(fact, session, readCommitted: false);
                    if (existingFacts.Count == 1)
                    {
                        session.Commit();

                        if (messages.Any() && PivotAffected != null)
                            foreach (var message in messages)
                                PivotAffected(domain, message.PivotId);
                        return id;
                    }
                    else
                    {
                        _retried = true;
                    }
                }
            }
        }

        public FactID? FindExistingFact(string domain, FactMemento fact)
        {
            using (var session = new Session(_connectionString))
            {
                FactID id;
                if (FindExistingFact(fact, out id, session, readCommitted: true))
                    return id;
                return null;
            }
        }

        public List<FactID> LoadRecentMessages(string domain, FactID pivotId, Guid clientGuid, TimestampID timestamp)
        {
            using (var session = new Session(_connectionString))
            {
                int clientId = SaveClient(session, clientGuid);
                session.Command.CommandText =
                    "SELECT TOP (20) FactId " +
                    "FROM Message " +
                    "WHERE PivotId = @PivotId " +
                    "AND FactId > @Timestamp " +
                    "AND ClientId != @ClientId " +
                    "ORDER BY FactId";
                AddParameter(session.Command, "@PivotId", pivotId.key);
                AddParameter(session.Command, "@Timestamp", timestamp.Key);
                AddParameter(session.Command, "@ClientId", clientId);
                using (IDataReader messageReader = session.Command.ExecuteReader())
                {
                    session.Command.Parameters.Clear();

                    return LoadIDsFromReader(messageReader).ToList();
                }
            }
        }

        private bool FindExistingFact(FactMemento memento, out FactID id, Session session, bool readCommitted)
        {
            var existingFacts = FindExistingFacts(memento, session, readCommitted);
            if (existingFacts.Count > 1)
                throw new CorrespondenceException(string.Format("More than one fact matched the given {0}.", memento.FactType));
            if (existingFacts.Count == 1)
            {
                id = existingFacts[0].Id;
                return true;
            }
            else
            {
                id = new FactID();
                return false;
            }
        }

        private List<IdentifiedFactMemento> FindExistingFacts(FactMemento memento, Session session, bool readCommitted)
        {
            int typeId = SaveType(session, memento.FactType);

            // Load all candidates that have the same hash code.
            session.Command.CommandText = HEAD_SELECT +
                (readCommitted ? "" : "WITH (NOLOCK) ") +
                "WHERE f.FKTypeID = @TypeID AND f.Hashcode = @Hashcode " +
                TAIL_JOIN +
                "ORDER BY ff.FactID, p.PredecessorID";
            AddParameter(session.Command, "@TypeID", typeId);
            AddParameter(session.Command, "@Hashcode", memento.GetHashCode());
            using (IDataReader factReader = session.Command.ExecuteReader())
            {
                session.Command.Parameters.Clear();

                return LoadMementosFromReader(factReader).Where(im => im.Memento.Equals(memento)).ToList();
            }
        }

        private void SaveMessages(Session session, IEnumerable<MessageMemento> messages, int clientId)
        {
            session.Command.CommandText = "INSERT INTO Message (FactId, PivotId, ClientId) VALUES (@FactId, @PivotId, @ClientId)";
            foreach (MessageMemento message in messages)
            {
                AddParameter(session.Command, "@FactId", message.FactId.key);
                AddParameter(session.Command, "@PivotId", message.PivotId.key);
                AddParameter(session.Command, "@ClientId", clientId);
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();
            }
        }

        private IEnumerable<IdentifiedFactMemento> LoadMementosFromReader(IDataReader factReader)
        {
            IdentifiedFactMemento current = null;

            while (factReader.Read())
            {
                // FactID, Data, TypeName, Version, DeclaringTypeName, DeclaringTypeVersion, RoleName, PredecessorFactID, IsPivot
                long factId = factReader.GetInt64(0);

                // Load the header.
                if (current == null || factId != current.Id.key)
                {
                    if (current != null)
                        yield return current;

                    string typeName = factReader.GetString(2);
                    int typeVersion = factReader.GetInt32(3);

                    // Create the memento.
                    current = new IdentifiedFactMemento(
                        new FactID() { key = factId },
                        new FactMemento(new CorrespondenceFactType(typeName, typeVersion)));
                    ReadBinary(factReader, current.Memento, 1);
                }

                // Load a predecessor.
                if (!factReader.IsDBNull(4))
                {
                    string declaringTypeName = factReader.GetString(4);
                    int declaringTypeVersion = factReader.GetInt32(5);
                    string roleName = factReader.GetString(6);
                    long predecessorFactId = factReader.GetInt64(7);
                    bool isPivot = factReader.GetBoolean(8);

                    current.Memento.AddPredecessor(
                        new RoleMemento(
                            new CorrespondenceFactType(
                                declaringTypeName,
                                declaringTypeVersion),
                            roleName,
                            null,
                            false),
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

        private int SaveClient(Session session, Guid clientGuid)
        {
            // See if the client already exists.
            int clientId = 0;
            session.Command.CommandText = "SELECT ClientId FROM Client WHERE ClientGuid = @ClientGuid";
            AddParameter(session.Command, "@ClientGuid", clientGuid);
            using (IDataReader typeReader = session.Command.ExecuteReader())
            {
                session.Command.Parameters.Clear();
                if (typeReader.Read())
                {
                    clientId = typeReader.GetInt32(0);
                }
            }

            // If not, create it.
            if (clientId == 0)
            {
                session.Command.CommandText = "INSERT INTO Client (ClientGuid) VALUES (@ClientGuid)";
                AddParameter(session.Command, "@ClientGuid", clientGuid);
                session.Command.ExecuteNonQuery();
                session.Command.Parameters.Clear();

                session.Command.CommandText = "SELECT @@IDENTITY";
                clientId = (int)(decimal)session.Command.ExecuteScalar();
                session.Command.Parameters.Clear();
            }

            return clientId;
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
