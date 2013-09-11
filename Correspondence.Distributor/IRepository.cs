using System;
using System.Collections.Generic;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public interface IRepository
    {
        event Delegates.PivotAffectedDelegate PivotAffected;

        FactMemento Load(string domain, FactID factId);
        FactID Save(string domain, FactMemento fact, Guid clientGuid);
        FactID? FindExistingFact(string domain, FactMemento fact);
        List<FactID> LoadRecentMessages(string domain, FactID pivotId, Guid clientGuid, TimestampID timestamp);
        void DeleteMessages(string domain, List<UnpublishMemento> unpublishMementos);

        void SaveWindowsPhoneSubscription(
            IEnumerable<FactID> pivotIds,
            string deviceUri,
            Guid clientGuid);
        List<WindowsPhoneSubscription> LoadWindowsPhoneSubscriptions(
            IEnumerable<FactID> pivotIds,
            Guid clientGuid);
        void DeleteWindowsPhoneSubscriptions(
            IEnumerable<FactID> pivotIds, string deviceUri);
        void DeleteWindowsPhoneSubscriptionsByDeviceId(
            IEnumerable<string> deviceUris);
    }
}
