using System;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public static class Delegates
    {
        public delegate void PivotAffectedDelegate(string domain, FactID pivotId, FactID factId, Guid clientGuid);
    }
}
