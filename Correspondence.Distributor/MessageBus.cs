using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            public Guid ClientGuid;
            public CancellationTokenSource Cancellation;
        }
        private List<Registration> _registrations = new List<Registration>();


        public void Register(
            string domain,
            List<FactID> pivotIds,
            Guid clientGuid,
            CancellationTokenSource cancellation)
        {
            lock (this)
            {
                foreach (var pivotId in pivotIds)
                {
                    Debug.WriteLine(String.Format("Register {0} {1}", clientGuid, pivotId));
                    _registrations.Add(new Registration
                    {
                        Domain = domain,
                        PivotId = pivotId,
                        ClientGuid = clientGuid,
                        Cancellation = cancellation
                    });
                }
            }
        }

        public void Unregister(CancellationTokenSource cancellation)
        {
            lock (this)
            {
                foreach (var registration in _registrations.Where(r => r.Cancellation == cancellation))
                    Debug.WriteLine(String.Format("Unregister {0} {1}", registration.ClientGuid, registration.PivotId));
                _registrations.RemoveAll(n => n.Cancellation == cancellation);
            }
        }

        public void Notify(string domain, Guid clientGuid)
        {
            List<Registration> registrations;
            lock (this)
            {
                Debug.WriteLine(String.Format("Notify all {0} of {1}", clientGuid, _registrations.Count));
                registrations = _registrations
                    .Where(n =>
                        n.Domain == domain &&
                        n.ClientGuid == clientGuid)
                    .ToList();
            }
            foreach (var registration in registrations)
            {
                Debug.WriteLine(String.Format("Notify {0} {1}", registration.ClientGuid, registration.PivotId));
                registration.Cancellation.Cancel();
            }
        }

        public void Notify(string domain, FactID pivotId)
        {
            List<Registration> registrations;
            lock (this)
            {
                Debug.WriteLine(String.Format("Notify all {0} of {1}", pivotId, _registrations.Count));
                registrations = _registrations
                    .Where(n =>
                        n.Domain == domain &&
                        n.PivotId.Equals(pivotId))
                    .ToList();
            }
            foreach (var registration in registrations)
            {
                Debug.WriteLine(String.Format("Notify {0} {1}", registration.ClientGuid, registration.PivotId));
                registration.Cancellation.Cancel();
            }
        }
    }
}
