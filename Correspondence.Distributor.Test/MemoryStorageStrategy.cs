using System;
using System.Collections.Generic;
using System.Linq;
using Correspondence.Distributor.Test.Records;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    public class MemoryStorageStrategy
    {
        private List<FactRecord> _factTable = new List<FactRecord>();
        private List<MessageRecord> _messageTable = new List<MessageRecord>();

        public FactMemento Load(FactID id)
        {
            lock (this)
            {
                FactRecord factRecord = _factTable.FirstOrDefault(o => o.IdentifiedFactMemento.Id.Equals(id));
                if (factRecord != null)
                    return factRecord.IdentifiedFactMemento.Memento;
                else
                    throw new CorrespondenceException(string.Format("Fact with id {0} not found.", id));
            }
        }

        public bool Save(FactMemento memento, int peerId, string source, out List<FactID> affectedPivots, out FactID id)
        {
            lock (this)
            {
                // See if the fact already exists.
                FactRecord fact = _factTable.FirstOrDefault(o => o.IdentifiedFactMemento.Memento.Equals(memento));
                if (fact == null)
                {
                    // It doesn't, so create it.
                    FactID newFactID = new FactID() { key = _factTable.Count + 1 };
                    id = newFactID;
                    fact = new FactRecord()
                    {
                        IdentifiedFactMemento = new IdentifiedFactMemento(id, memento),
                        PeerId = peerId
                    };

                    _factTable.Add(fact);

                    // Store a message for each pivot.
                    var pivots = memento.Predecessors
                        .Where(predecessor => predecessor.IsPivot);
                    _messageTable.AddRange(pivots
                        .Select(predecessor => new MessageRecord(
                            new MessageMemento(predecessor.ID, newFactID),
                            newFactID,
                            predecessor.Role,
                            source)));

                    // Store messages for each non-pivot. This fact belongs to all predecessors' pivots.
                    List<FactID> nonPivots = memento.Predecessors
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
                            source)));

                    affectedPivots =
                        pivots
                            .Select(pivot => pivot.ID)
                        .Union(predecessorsPivots
                            .Select(predecessorPivot => predecessorPivot.Message.PivotId))
                        .ToList();
                    return true;
                }
                else
                {
                    id = fact.IdentifiedFactMemento.Id;
                    affectedPivots = null;
                    return false;
                }
            }
        }

        public bool FindExistingFact(FactMemento memento, out FactID id)
        {
            lock (this)
            {
                // See if the fact already exists.
                FactRecord fact = _factTable.FirstOrDefault(o => o.IdentifiedFactMemento.Memento.Equals(memento));
                if (fact == null)
                {
                    id = new FactID();
                    return false;
                }
                else
                {
                    id = fact.IdentifiedFactMemento.Id;
                    return true;
                }
            }
        }

        public IEnumerable<FactID> LoadRecentMessagesForClient(FactID pivotId, TimestampID timestamp, string target)
        {
            return _messageTable
                .Where(message =>
                    message.Message.PivotId.Equals(pivotId) &&
                    message.Message.FactId.key > timestamp.Key &&
                    message.Source != target)
                .Select(message => message.Message.FactId)
                .Distinct();
        }

        public void Unpublish(FactID factId, RoleMemento role)
        {
            _messageTable.RemoveAll(message =>
                message.AncestorFact.Equals(factId) &&
                message.AncestorRole.Equals(role));
        }
    }
}
