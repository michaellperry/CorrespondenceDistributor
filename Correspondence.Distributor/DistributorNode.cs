using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public class DistributorNode
    {
        private readonly DistributorService _service;

        struct PivotNotification
        {
            public string Domain;
            public FactID PivotId;
            public CancellationTokenSource Cancelation;
        }
        private List<PivotNotification> _notifications;

        public DistributorNode(DistributorService service)
        {
            _service = service;
            _notifications = new List<PivotNotification>();
            _service.PivotAffected += Service_PivotAffected;
        }

        public Task<GetManyResult> GetMany(
            string clientGuid,
            string domain,
            FactTreeMemento tree,
            Dictionary<long, long> pivotIds,
            int timeoutSeconds)
        {
            var result = new GetManyResult();
            result.Tree = _service.GetMany(clientGuid, domain, tree, pivotIds);
            if (timeoutSeconds > 0 && !result.Tree.Facts.Any())
            {
                CancellationTokenSource cancelation = new CancellationTokenSource();
                foreach (long pivotId in pivotIds.Keys)
                {
                    _notifications.Add(new PivotNotification
                    {
                        Domain = domain,
                        PivotId = new FactID { key = pivotId },
                        Cancelation = cancelation
                    });
                }
                return Task
                    .Delay(timeoutSeconds * 1000, cancelation.Token)
                    .ContinueWith(t => result);
            }

            return Task.FromResult(result);
        }

        public void Post(string clientGuid, string domain, FactTreeMemento factTree, List<UnpublishMemento> unpublishMementos)
        {
            _service.Post(clientGuid, domain, factTree, unpublishMementos);
        }

        private void Service_PivotAffected(string domain, FactID pivotId)
        {
            var notifications = _notifications
                .Where(n => n.Domain == domain && n.PivotId.Equals(pivotId));
            foreach (var notification in notifications)
                notification.Cancelation.Cancel();
        }
    }
}
