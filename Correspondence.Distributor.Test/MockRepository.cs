using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UpdateControls.Correspondence.Mementos;
using UpdateControls.Correspondence.Memory;

namespace Correspondence.Distributor.Test
{
    public class MockRepository : IRepository
    {
        private MemoryStorageStrategy _memory;
        private Dictionary<FactID, string> _sourceByFact = new Dictionary<FactID, string>();

        public MockRepository()
        {
            _memory = new MemoryStorageStrategy();
        }

        public long AddFact(string domain, FactMemento fact)
        {
            FactID factId;
            _memory.Save(fact, 0, out factId);
            return factId.key;
        }

        public FactMemento Load(string domain, FactID factId)
        {
            return _memory.Load(factId);
        }

        public FactID Save(string domain, FactMemento fact, string clientGuid)
        {
            FactID factId;
            bool saved = _memory.Save(fact, 0, out factId);
            if (saved)
                _sourceByFact.Add(factId, clientGuid);
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
                .LoadRecentMessagesForClient(localPivotId, timestamp)
                .Where(id => IsNotForClient(id, clientGuid))
                .ToList();
        }

        private bool IsNotForClient(FactID id, string clientGuid)
        {
            string sourceClientGuid;
            if (!_sourceByFact.TryGetValue(id, out sourceClientGuid))
                return true;
            return sourceClientGuid != clientGuid;
        }
    }
}
