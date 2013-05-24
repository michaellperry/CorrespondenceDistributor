using System.Collections.Generic;
using System.Linq;
using Correspondence.Distributor.Test.Records;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    public class MockRepository : IRepository
    {
        private List<FactRecord> _factTable = new List<FactRecord>();
        private List<MessageRecord> _messageTable = new List<MessageRecord>();

        public FactMemento Load(string domain, FactID factId)
        {
            lock (this)
            {
                FactRecord factRecord = _factTable.FirstOrDefault(o =>
                    o.IdentifiedFactMemento.Id.Equals(factId));
                if (factRecord != null)
                    return factRecord.IdentifiedFactMemento.Memento;
                else
                    throw new CorrespondenceException(
                        string.Format("Fact with id {0} not found.", factId));
            }
        }

        public FactID Save(string domain, FactMemento fact, string clientGuid)
        {
            FactID factId;
            List<FactID> affectedPivots;

            lock (this)
            {
                // See if the fact already exists.
                FactRecord existingFact = _factTable.FirstOrDefault(o =>
                    o.IdentifiedFactMemento.Memento.Equals(fact));
                if (existingFact == null)
                {
                    // It doesn't, so create it.
                    FactID newFactID = new FactID() { key = _factTable.Count + 1 };
                    factId = newFactID;
                    existingFact = new FactRecord()
                    {
                        IdentifiedFactMemento = new IdentifiedFactMemento(factId, fact)
                    };

                    _factTable.Add(existingFact);

                    // Store a message for each pivot.
                    var pivots = fact.Predecessors
                        .Where(predecessor => predecessor.IsPivot);
                    _messageTable.AddRange(pivots
                        .Select(predecessor => new MessageRecord(
                            new MessageMemento(predecessor.ID, newFactID),
                            newFactID,
                            predecessor.Role,
                            clientGuid)));

                    // Store messages for each non-pivot. This fact belongs to all predecessors' pivots.
                    List<FactID> nonPivots = fact.Predecessors
                        .Where(predecessor => !predecessor.IsPivot)
                        .Select(predecessor => predecessor.ID)
                        .ToList();
                    List<MessageRecord> predecessorsPivots = _messageTable
                        .Where(message => nonPivots.Contains(message.Message.FactId))
                        .Distinct()
                        .ToList();
                    _messageTable.AddRange(predecessorsPivots
                        .Select(predecessorPivot => new MessageRecord(
                            new MessageMemento(predecessorPivot.Message.PivotId, newFactID),
                            predecessorPivot.AncestorFact,
                            predecessorPivot.AncestorRole,
                            clientGuid)));

                    affectedPivots =
                        pivots
                            .Select(pivot => pivot.ID)
                        .Union(predecessorsPivots
                            .Select(predecessorPivot => predecessorPivot.Message.PivotId))
                        .ToList();
                }
                else
                {
                    factId = existingFact.IdentifiedFactMemento.Id;
                    affectedPivots = null;
                }
            }

            if (affectedPivots != null && affectedPivots.Any() && PivotAffected != null)
                foreach (var pivotId in affectedPivots)
                    PivotAffected(domain, pivotId);

            return factId;
        }

        public FactID? FindExistingFact(string domain, FactMemento memento)
        {
            lock (this)
            {
                // See if the fact already exists.
                FactRecord fact = _factTable.FirstOrDefault(o =>
                    o.IdentifiedFactMemento.Memento.Equals(memento));
                if (fact == null)
                    return null;
                else
                    return fact.IdentifiedFactMemento.Id;
            }
        }

        public List<FactID> LoadRecentMessages(string domain, FactID localPivotId, string clientGuid, TimestampID timestamp)
        {
            return _messageTable.Where(message => message.Message.PivotId.Equals(localPivotId) && message.Message.FactId.key > timestamp.Key && message.Source != clientGuid).Select(message => message.Message.FactId).Distinct()
                .ToList();
        }

        public event Delegates.PivotAffectedDelegate PivotAffected;

        private void Unpublish(FactID factId, RoleMemento role)
        {
            _messageTable.RemoveAll(message =>
                message.AncestorFact.Equals(factId) &&
                message.AncestorRole.Equals(role));
        }
    }
}
