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
            public CancellationTokenSource Cancellation;
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
            GetManyResult result = GetManyFromService(clientGuid, domain, tree, pivotIds);
            if (timeoutSeconds > 0 && !result.Tree.Facts.Any())
            {
                CancellationTokenSource cancellation = new CancellationTokenSource();
                AddNotifications(domain, result.LocalPivotIds, cancellation);
                return Task
                    .Delay(timeoutSeconds * 1000, cancellation.Token)
                    .ContinueWith(t => t.IsCanceled
                        ? GetManyFromService(clientGuid, domain, tree, pivotIds)
                        : result)
                    .ContinueWith(t =>
                    {
                        RemoveNotifications(cancellation);
                        cancellation.Dispose();
                        return t.Result;
                    });
            }

            return Task.FromResult(result);
        }

        public void Post(string clientGuid, string domain, FactTreeMemento factTree, List<UnpublishMemento> unpublishMementos)
        {
            _service.Post(clientGuid, domain, factTree, unpublishMementos);
        }

        private GetManyResult GetManyFromService(string clientGuid, string domain, FactTreeMemento tree, Dictionary<long, long> pivotIds)
        {
            var result = new GetManyResult();
            var localPivotIds = new List<FactID>();
            result.Tree = _service.GetMany(clientGuid, domain, tree, pivotIds, localPivotIds);
            result.PivotIds = pivotIds;
            result.LocalPivotIds = localPivotIds;
            return result;
        }

        private void Service_PivotAffected(string domain, FactID pivotId)
        {
            var notifications = _notifications
                .Where(n => n.Domain == domain && n.PivotId.Equals(pivotId));
            foreach (var notification in notifications)
                notification.Cancellation.Cancel();
        }

        private void AddNotifications(
            string domain,
            List<FactID> pivotIds,
            CancellationTokenSource cancellation)
        {
            lock (this)
            {
                foreach (var pivotId in pivotIds)
                {
                    _notifications.Add(new PivotNotification
                    {
                        Domain = domain,
                        PivotId = pivotId,
                        Cancellation = cancellation
                    });
                }
            }
        }

        private void RemoveNotifications(CancellationTokenSource cancellation)
        {
            lock (this)
            {
                _notifications.RemoveAll(n => n.Cancellation == cancellation);
            }
        }
    }
}
