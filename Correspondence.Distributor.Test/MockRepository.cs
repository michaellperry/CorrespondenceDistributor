using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    public class MockRepository : IRepository
    {
        private MemoryStorageStrategy _memory;

        public MockRepository()
        {
            _memory = new MemoryStorageStrategy();
        }

        public long AddFact(string domain, FactMemento fact)
        {
            FactID factId;
            List<FactID> affectedPivots;
            _memory.Save(fact, 0, null, out affectedPivots, out factId);
            return factId.key;
        }

        public FactMemento Load(string domain, FactID factId)
        {
            return _memory.Load(factId);
        }

        public FactID Save(string domain, FactMemento fact, string clientGuid)
        {
            FactID factId;
            List<FactID> affectedPivots;
            bool saved = _memory.Save(fact, 0, clientGuid, out affectedPivots, out factId);
            if (saved && affectedPivots.Any() && PivotAffected != null)
                foreach (var pivotId in affectedPivots)
                    PivotAffected(domain, pivotId);
            return factId;
        }

        public FactID? FindExistingFact(string domain, FactMemento translatedMemento)
        {
            FactID factId;
            if (!_memory.FindExistingFact(translatedMemento, out factId))
                return null;

            return factId;
        }

        public List<FactID> LoadRecentMessages(string domain, FactID localPivotId, string clientGuid, TimestampID timestamp)
        {
            return _memory
                .LoadRecentMessagesForClient(localPivotId, timestamp, clientGuid)
                .ToList();
        }

        public event Delegates.PivotAffectedDelegate PivotAffected;
    }
}
