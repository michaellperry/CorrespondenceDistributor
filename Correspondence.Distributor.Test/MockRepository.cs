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

        public MockRepository()
        {
            _memory = new MemoryStorageStrategy();
        }

        public long AddFact(FactMemento fact)
        {
            FactID factId;
            _memory.Save(fact, 0, out factId);
            return factId.key;
        }

        public FactMemento Load(FactID factId)
        {
            return _memory.Load(factId);
        }

        public FactID Save(FactMemento fact)
        {
            FactID factId;
            _memory.Save(fact, 0, out factId);
            return factId;
        }

        public FactID? FindExistingFact(FactMemento translatedMemento)
        {
            FactID factId;
            if (!_memory.FindExistingFact(translatedMemento, out factId))
                return null;

            return factId;
        }

        public List<FactID> LoadRecentMessages(FactID localPivotId, TimestampID timestamp)
        {
            return _memory
                .LoadRecentMessagesForClient(localPivotId, timestamp)
                .ToList();
        }
    }
}
