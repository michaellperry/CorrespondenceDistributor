using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public class DistributorService
    {
        public FactTreeMemento GetMany(
            string clientGuid, 
            string domain, 
            FactTreeMemento pivotTree, 
            Dictionary<long, long> pivotIds, 
            int timeoutSeconds)
        {
            throw new NotImplementedException();
        }

        public void Post(
            string clientGuid, 
            string domain, 
            FactTreeMemento factTree, 
            List<UnpublishMemento> unpublishMessages)
        {
            throw new NotImplementedException();
        }

        public void Interrupt(
            string clientGuid,
            string domain)
        {
            throw new NotImplementedException();
        }

        public void Notify(
            string clientGuid,
            string domain, 
            FactTreeMemento pivotTree, 
            long pivotId, 
            string text1, 
            string text2)
        {
            throw new NotImplementedException();
        }

        public void WindowsSubscribe(
            string clientGuid,
            string domain,
            FactTreeMemento pivotTree,
            long pivotId,
            string deviceUri)
        {
            throw new NotImplementedException();
        }

        public void WindowsUnsubscribe(
            string domain,
            FactTreeMemento pivotTree,
            long pivotId,
            string deviceUri)
        {
            throw new NotImplementedException();
        }
    }
}
