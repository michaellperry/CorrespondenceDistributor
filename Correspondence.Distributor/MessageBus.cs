using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    class MessageBus
    {
        struct Registration
        {
            public string Domain;
            public FactID PivotId;
            public CancellationTokenSource Cancellation;
        }
        private List<Registration> _registrations = new List<Registration>();


        public void Register(
            string domain,
            List<FactID> pivotIds,
            CancellationTokenSource cancellation)
        {
            lock (this)
            {
                foreach (var pivotId in pivotIds)
                {
                    _registrations.Add(new Registration
                    {
                        Domain = domain,
                        PivotId = pivotId,
                        Cancellation = cancellation
                    });
                }
            }
        }

        public void Unregister(CancellationTokenSource cancellation)
        {
            lock (this)
            {
                _registrations.RemoveAll(n => n.Cancellation == cancellation);
            }
        }

        public void Notify(string domain, FactID pivotId)
        {
            var notifications = _registrations
                .Where(n => n.Domain == domain && n.PivotId.Equals(pivotId));
            foreach (var notification in notifications)
                notification.Cancellation.Cancel();
        }
    }
}
