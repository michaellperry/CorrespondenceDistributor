using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public class GetManyResult
    {
        public FactTreeMemento Tree { get; set; }
        public Dictionary<long, long> PivotIds { get; set; }
        public List<FactID> LocalPivotIds { get; set; }
    }
}
