using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public class DistributorService
    {
        private readonly IRepository _repository;
        private readonly MessageBus _messageBus;

        public DistributorService(IRepository repository)
        {
            _repository = repository;
            _repository.PivotAffected += Repository_PivotAffected;
            _messageBus = new MessageBus();
        }

        public Task<GetManyResult> GetManyAsync(
            Guid clientGuid,
            string domain,
            FactTreeMemento tree,
            Dictionary<long, long> pivotIds,
            int timeoutSeconds)
        {
            GetManyResult result = GetManyInternal(clientGuid, domain, tree, pivotIds);
            if (timeoutSeconds > 0 && !result.Tree.Facts.Any())
            {
                CancellationTokenSource cancellation = new CancellationTokenSource();
                int session = _messageBus.Register(
                    domain,
                    result.LocalPivotIds,
                    clientGuid,
                    () => cancellation.Cancel());
                return Task
                    .Delay(timeoutSeconds * 1000, cancellation.Token)
                    .ContinueWith(t => t.IsCanceled
                        ? GetManyInternal(clientGuid, domain, tree, pivotIds)
                        : result)
                    .ContinueWith(t =>
                    {
                        _messageBus.Unregister(session);
                        cancellation.Dispose();
                        return t.Result;
                    });
            }

            return Task.FromResult(result);
        }

        private GetManyResult GetManyInternal(Guid clientGuid, string domain, FactTreeMemento tree, Dictionary<long, long> pivotIds)
        {
            var localPivotIds = new List<FactID>();
            FactTreeMemento messageBody = new FactTreeMemento(0);
            Dictionary<FactID, FactID> localIdByRemoteId = ForEachFact(tree, fact =>
                _repository.Save(domain, fact, clientGuid));
            Dictionary<long, long> newPivotIds = new Dictionary<long, long>();
            foreach (var pivot in pivotIds)
            {
                long remotePivotId = pivot.Key;
                FactID localPivotId;
                long pivotValue = pivot.Value;
                TimestampID timestamp = new TimestampID(0, pivotValue);
                if (localIdByRemoteId.TryGetValue(new FactID { key = remotePivotId }, out localPivotId))
                {
                    List<FactID> recentMessages = _repository.LoadRecentMessages(domain, localPivotId, clientGuid, timestamp);
                    foreach (FactID recentMessage in recentMessages)
                    {
                        AddToFactTree(domain, messageBody, recentMessage, localIdByRemoteId);
                        if (recentMessage.key > pivotValue)
                            pivotValue = recentMessage.key;
                    }
                    newPivotIds[remotePivotId] = pivotValue;
                    localPivotIds.Add(localPivotId);
                }
            }
            foreach (var pivot in newPivotIds)
                pivotIds[pivot.Key] = pivot.Value;
            var result = new GetManyResult()
            {
                Tree = messageBody,
                PivotIds = pivotIds,
                LocalPivotIds = localPivotIds
            };
            return result;
        }

        public void Post(
            Guid clientGuid,
            string domain,
            FactTreeMemento factTree,
            List<UnpublishMemento> unpublishMessages)
        {
            Dictionary<FactID, FactID> localIdByRemoteId = ForEachFact(factTree,
                fact => _repository.Save(domain, fact, clientGuid));
            var unpublishMementos = unpublishMessages
                .SelectMany(m => MapUnpublishMemento(localIdByRemoteId, m))
                .ToList();
            _repository.DeleteMessages(domain, unpublishMementos);
        }

        private IEnumerable<UnpublishMemento> MapUnpublishMemento(Dictionary<FactID, FactID> localIdByRemoteId, UnpublishMemento remoteUnpublish)
        {
            FactID localId;
            if (localIdByRemoteId.TryGetValue(remoteUnpublish.MessageId, out localId))
                yield return new UnpublishMemento(localId, remoteUnpublish.Role);
        }

        public void Interrupt(
            Guid clientGuid,
            string domain)
        {
            _messageBus.Notify(domain, clientGuid);
        }

        public void Notify(
            Guid clientGuid,
            string domain,
            FactTreeMemento pivotTree,
            long pivotId,
            string text1,
            string text2)
        {
            throw new NotImplementedException();
        }

        public void WindowsPhoneSubscribe(
            Guid clientGuid,
            string domain,
            FactTreeMemento factTree,
            long pivotId,
            string deviceUri)
        {
            Dictionary<FactID, FactID> localIdByRemoteId = ForEachFact(factTree,
                fact => _repository.Save(domain, fact, clientGuid));
            FactID localId;
            if (localIdByRemoteId.TryGetValue(new FactID { key = pivotId }, out localId))
            {
                _repository.SaveWindowsPhoneSubscription(
                    new List<FactID> { localId },
                    deviceUri,
                    clientGuid);
            }
        }

        public void WindowsPhoneUnsubscribe(
            string domain,
            FactTreeMemento factTree,
            long pivotId,
            string deviceUri)
        {
            Dictionary<FactID, FactID> localIdByRemoteId = ForEachFact(factTree,
                fact => _repository.FindExistingFact(domain, fact));
            FactID localId;
            if (localIdByRemoteId.TryGetValue(new FactID { key = pivotId }, out localId))
            {
                _repository.DeleteWindowsPhoneSubscriptions(
                    new List<FactID> { localId },
                    deviceUri);
            }
        }

        private static Dictionary<FactID, FactID> ForEachFact(FactTreeMemento tree, Func<FactMemento, FactID?> processFact)
        {
            Dictionary<FactID, FactID> localIdByRemoteId = new Dictionary<FactID, FactID>();
            foreach (IdentifiedFactMemento identifiedFact in tree.Facts)
            {
                FactMemento translatedMemento = TranslateMemento(localIdByRemoteId, identifiedFact);
                if (translatedMemento != null)
                {
                    FactID? localId = processFact(translatedMemento);
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

        private void AddToFactTree(string domain, FactTreeMemento messageBody, FactID factId, Dictionary<FactID, FactID> localIdByRemoteId)
        {
            if (!messageBody.Contains(factId))
            {
                FactMemento fact = _repository.Load(domain, factId);
                foreach (PredecessorMemento predecessor in fact.Predecessors)
                    AddToFactTree(domain, messageBody, predecessor.ID, localIdByRemoteId);
                messageBody.Add(new IdentifiedFactMemento(factId, fact));
            }
        }

        private void Repository_PivotAffected(string domain, FactID pivotId, FactID factId, Guid clientGuid)
        {
            _messageBus.Notify(domain, pivotId);
        }
    }
}
