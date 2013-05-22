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
        FactID Save(FactMemento fact);
        FactID? FindExistingFact(FactMemento fact);
        List<FactID> LoadRecentMessages(FactID pivotId, TimestampID timestamp);
    }
}
