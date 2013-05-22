using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public interface IRepository
    {
        FactMemento Load(FactID factId);
        FactID? FindExistingFact(FactMemento translatedMemento);
        List<FactID> LoadRecentMessages(FactID localPivotId, TimestampID timestamp);
    }
}
