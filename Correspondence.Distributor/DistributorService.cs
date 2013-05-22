using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public class DistributorService
    {
        private IRepository _repository;

        public DistributorService(IRepository repository)
        {
            _repository = repository;
        }

        public FactTreeMemento GetMany(
            string clientGuid,
            string domain,
            FactTreeMemento pivotTree,
            Dictionary<long, long> pivotIds,
            int timeoutSeconds)
        {
            FactTreeMemento messageBody = new FactTreeMemento(0);
            Dictionary<FactID, FactID> localIdByRemoteId = FindExistingFacts(pivotTree);
            Dictionary<long, long> newPivotIds = new Dictionary<long, long>();
            foreach (var pivot in pivotIds)
            {
                long pivotId = pivot.Key;
                FactID localPivotId;
                long pivotValue = pivot.Value;
                TimestampID timestamp = new TimestampID(0, pivotValue);
                if (localIdByRemoteId.TryGetValue(new FactID { key = pivotId }, out localPivotId))
                {
                    List<FactID> recentMessages = _repository.LoadRecentMessages(localPivotId, timestamp);
                    foreach (FactID recentMessage in recentMessages)
                    {
                        AddToFactTree(messageBody, recentMessage, localIdByRemoteId);
                        if (recentMessage.key > pivotValue)
                            pivotValue = recentMessage.key;
                    }
                    newPivotIds[pivotId] = pivotValue;
                }
            }

            foreach (var pivot in newPivotIds)
                pivotIds[pivot.Key] = pivot.Value;

            return messageBody;
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

        private Dictionary<FactID, FactID> FindExistingFacts(FactTreeMemento pivotTree)
        {
            Dictionary<FactID, FactID> localIdByRemoteId = new Dictionary<FactID, FactID>();
            foreach (IdentifiedFactMemento identifiedFact in pivotTree.Facts)
            {
                FactMemento translatedMemento = TranslateMemento(localIdByRemoteId, identifiedFact);
                if (translatedMemento != null)
                {
                    FactID? localId = _repository.FindExistingFact(translatedMemento);
                    if (localId != null)
                    {
                        localIdByRemoteId.Add(identifiedFact.Id, localId.Value);
                    }
                }
            }
            return localIdByRemoteId;
        }

        private static FactMemento TranslateMemento(Dictionary<FactID, FactID> localIdByRemoteId, IdentifiedFactMemento identifiedFact)
        {
            FactMemento translatedMemento = new FactMemento(identifiedFact.Memento.FactType);
            translatedMemento.Data = identifiedFact.Memento.Data;
            foreach (PredecessorMemento remote in identifiedFact.Memento.Predecessors)
            {
                FactID localPredecessorId;
                if (!localIdByRemoteId.TryGetValue(remote.ID, out localPredecessorId))
                    return null;

                translatedMemento.AddPredecessor(remote.Role, localPredecessorId, remote.IsPivot);
            }
            return translatedMemento;
        }

        private void AddToFactTree(FactTreeMemento messageBody, FactID factId, Dictionary<FactID, FactID> localIdByRemoteId)
        {
            if (!messageBody.Contains(factId))
            {
                FactMemento fact = _repository.Load(factId);
                foreach (PredecessorMemento predecessor in fact.Predecessors)
                    AddToFactTree(messageBody, predecessor.ID, localIdByRemoteId);
                messageBody.Add(new IdentifiedFactMemento(factId, fact));
            }
        }
    }
}
