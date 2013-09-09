using System;
using System.Collections.Generic;
using System.Linq;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    public class Repository : IRepository
    {
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

            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                identifiedMemento = procedures.GetIdentifiedMemento(factId);
            }
            if (identifiedMemento == null)
                throw new CorrespondenceException(string.Format("Unable to find fact {0}", factId.key));

            return identifiedMemento.Memento;
        }

        public FactID Save(string domain, FactMemento fact, Guid clientGuid)
        {
            // Retry on concurrency failure.
            while (true)
            {
                using (var procedures = new Procedures(new Session(_connectionString)))
                {
                    procedures.BeginTransaction();

                    // First see if the fact is already in storage.
                    FactID id;
                    if (FindExistingFact(fact, out id, procedures, readCommitted: true))
                        return id;

                    // It isn't there, so store it.
                    int typeId = SaveType(procedures, fact.FactType);
                    id = procedures.InsertFact(fact, typeId);

                    // Store the predecessors.
                    foreach (PredecessorMemento predecessor in fact.Predecessors)
                    {
                        int roleId = SaveRole(procedures, predecessor.Role);
                        procedures.InsertPredecessor(id, predecessor, roleId);
                    }

                    // Store a message for each pivot.
                    FactID newFactId = id;
                    List<AncestorMessage> pivotMessages = fact.Predecessors
                        .Where(predecessor => predecessor.IsPivot)
                        .Select(predecessor =>
                            new AncestorMessage
                            {
                                AncestorFactId = newFactId,
                                AncestorRoleId = SaveRole(procedures, predecessor.Role),
                                Message = new MessageMemento(predecessor.ID, newFactId)
                            })
                        .ToList();

                    // Store messages for each non-pivot. This fact belongs to all predecessors' pivots.
                    string[] nonPivots = fact.Predecessors
                        .Where(predecessor => !predecessor.IsPivot)
                        .Select(predecessor => predecessor.ID.key.ToString())
                        .ToArray();
                    List<AncestorMessage> nonPivotMessages;
                    if (nonPivots.Length > 0)
                    {
                        var predecessorsPivotRoles = procedures.GetAncestorPivots(nonPivots);
                        nonPivotMessages = predecessorsPivotRoles
                            .Select(predecessorPivot =>
                                new AncestorMessage
                                {
                                    AncestorFactId = predecessorPivot.AncestorFactId,
                                    AncestorRoleId = predecessorPivot.AncestorRoleId,
                                    Message = new MessageMemento(predecessorPivot.PivotId, newFactId)
                                })
                            .ToList();
                    }
                    else
                        nonPivotMessages = new List<AncestorMessage>();

                    int clientId = SaveClient(procedures, clientGuid);
                    var roleMessages = pivotMessages.Union(nonPivotMessages).Distinct().ToList();
                    procedures.InsertMessages(roleMessages, clientId);

                    // Optimistic concurrency check.
                    // Make sure we don't find more than one.
                    var existingFacts = FindExistingFacts(fact, procedures, readCommitted: false);
                    if (existingFacts.Count == 1)
                    {
                        procedures.Commit();

                        if (roleMessages.Any() && PivotAffected != null)
                            foreach (var roleMessage in roleMessages)
                                PivotAffected(domain, roleMessage.Message.PivotId, roleMessage.Message.FactId, clientGuid);
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
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                FactID id;
                if (FindExistingFact(fact, out id, procedures, readCommitted: true))
                    return id;
                return null;
            }
        }

        public List<FactID> LoadRecentMessages(string domain, FactID pivotId, Guid clientGuid, TimestampID timestamp)
        {
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                int clientId = SaveClient(procedures, clientGuid);
                return procedures.GetRecentMessages(pivotId, timestamp, clientId);
            }
        }

        public void DeleteMessages(string domain, List<UnpublishMemento> unpublishMementos)
        {
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                foreach (var unpublish in unpublishMementos)
                {
                    int roleId = SaveRole(procedures, unpublish.Role);
                    procedures.DeleteMessage(unpublish.MessageId, roleId);
                }
            }
        }

        public void SaveWindowsPhoneSubscription(
            IEnumerable<FactID> pivotIds, 
            string deviceUri, 
            Guid clientGuid)
        {
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                int clientId = SaveClient(procedures, clientGuid);
                procedures.InsertWindowsPhoneSubscriptions(pivotIds, deviceUri, clientId);
            }
        }

        public List<WindowsPhoneSubscription> LoadWindowsPhoneSubscriptions(
            IEnumerable<FactID> pivotIds,
            Guid clientGuid)
        {
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                int clientId = SaveClient(procedures, clientGuid);
                return procedures.GetWindowsPhoneSubscriptionsByPivot(pivotIds, clientId);
            }
        }

        public void DeleteWindowsPhoneSubscriptions(IEnumerable<FactID> pivotIds, string deviceUri)
        {
            using (var procedures = new Procedures(new Session(_connectionString)))
            {
                procedures.DeleteWindowsPhoneSubscriptions(pivotIds, deviceUri);
            }
        }

        private bool FindExistingFact(FactMemento memento, out FactID id, Procedures procedures, bool readCommitted)
        {
            var existingFacts = FindExistingFacts(memento, procedures, readCommitted);
            if (existingFacts.Count > 1)
                throw new CorrespondenceException(string.Format(
                    "More than one fact matched the given {0}.",
                    memento.FactType));
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

        private List<IdentifiedFactMemento> FindExistingFacts(FactMemento memento, Procedures procedures, bool readCommitted)
        {
            int typeId = SaveType(procedures, memento.FactType);

            // Load all candidates that have the same hash code.
            return procedures.GetEqualFactsByHashCode(memento, readCommitted, typeId);
        }

        private int SaveRole(Procedures procedures, RoleMemento roleMemento)
        {
            int declaringTypeId = SaveType(procedures, roleMemento.DeclaringType);

            // See if the role already exists.
            int roleId = procedures.GetRoleId(roleMemento, declaringTypeId);

            // If not, create it.
            if (roleId == 0)
            {
                roleId = procedures.InsertRole(roleMemento, declaringTypeId);
            }

            return roleId;
        }

        private int SaveType(Procedures procedures, CorrespondenceFactType typeMemento)
        {
            // See if the type already exists.
            int typeId = procedures.GetTypeId(typeMemento);

            // If not, create it.
            if (typeId == 0)
            {
                typeId = procedures.InsertType(typeMemento);
            }

            return typeId;
        }

        private int SaveClient(Procedures procedures, Guid clientGuid)
        {
            // See if the client already exists.
            int clientId = procedures.GetClientId(clientGuid);

            // If not, create it.
            if (clientId == 0)
            {
                clientId = procedures.InsertClient(clientGuid);
            }

            return clientId;
        }

        public Repository UpgradeDatabase()
        {
            using (var session = new Session(_connectionString))
            {
                ScriptRunner.ExecuteAllScripts(session);
            }
            return this;
        }
    }
}
