using System;
using System.Collections.Generic;
using System.Linq;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    class MessageBus
    {
        struct Registration
        {
            public int Session;
            public string Domain;
            public FactID PivotId;
            public Guid ClientGuid;
            public Action Notify;
        }
        private List<Registration> _registrations = new List<Registration>();
        private int _nextSession = 0;

        public int Register(
            string domain,
            List<FactID> pivotIds,
            Guid clientGuid,
            Action notify)
        {
            lock (this)
            {
                int session = _nextSession++;
                foreach (var pivotId in pivotIds)
                {
                    _registrations.Add(new Registration
                    {
                        Session = session,
                        Domain = domain,
                        PivotId = pivotId,
                        ClientGuid = clientGuid,
                        Notify = notify
                    });
                }
                return session;
            }
        }

        public void Unregister(int session)
        {
            lock (this)
            {
                _registrations.RemoveAll(n => n.Session == session);
            }
        }

        public void Notify(string domain, Guid clientGuid)
        {
            List<Registration> registrations;
            lock (this)
            {
                registrations = _registrations
                    .Where(n =>
                        n.Domain == domain &&
                        n.ClientGuid == clientGuid)
                    .ToList();
            }
            foreach (var registration in registrations)
            {
                registration.Notify();
            }
        }

        public void Notify(string domain, FactID pivotId)
        {
            List<Registration> registrations;
            lock (this)
            {
                registrations = _registrations
                    .Where(n =>
                        n.Domain == domain &&
                        n.PivotId.Equals(pivotId))
                    .ToList();
            }
            foreach (var registration in registrations)
            {
                registration.Notify();
            }
        }
    }
}
