using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    public class Repository : IRepository
    {
        public event Delegates.PivotAffectedDelegate PivotAffected;

        public FactMemento Load(string domain, FactID factId)
        {
            throw new NotImplementedException();
        }

        public FactID Save(string domain, FactMemento fact, string clientGuid)
        {
            throw new NotImplementedException();
        }

        public FactID? FindExistingFact(string domain, FactMemento fact)
        {
            throw new NotImplementedException();
        }

        public List<FactID> LoadRecentMessages(string domain, FactID pivotId, string clientGuid, TimestampID timestamp)
        {
            throw new NotImplementedException();
        }
    }
}
