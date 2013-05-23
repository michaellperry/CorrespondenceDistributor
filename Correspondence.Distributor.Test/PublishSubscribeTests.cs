using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;
using System.Collections.Generic;
using System.Linq;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class PublishSubscribeTests : TestBase
    {
        private DistributorService _service;

        [TestInitialize]
        public void Initialize()
        {
            _service = new DistributorService(_mockRepository);
        }

        [TestMethod]
        public void CanGetWithNoPivots()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds);
            Assert.IsFalse(result.Facts.Any());
        }

        [TestMethod]
        public void CanGetPublishedFact()
        {
            long domainId = AddDomain();
            long roomId = AddRoom(domainId);

            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 4124 },
                new FactMemento(TypeDomain)));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds.Add(4124, 0);

            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds);
            Assert.AreEqual(2, result.Facts.Count());
            IdentifiedFactRemote  resultDomain = (IdentifiedFactRemote) result.Facts.ElementAt(0);
            IdentifiedFactMemento resultRoom   = (IdentifiedFactMemento)result.Facts.ElementAt(1);
            Assert.AreEqual(4124, resultDomain.RemoteId.key);
            Assert.AreEqual(TypeRoom, resultRoom.Memento.FactType);
            Assert.AreEqual(resultDomain.Id, resultRoom.Memento.Predecessors.ElementAt(0).ID);
            Assert.AreEqual(roomId, pivotIds[4124]);
        }

        [TestMethod]
        public void SkipsFactsAlreadyReceived()
        {
            long domainId = AddDomain();
            long roomId = AddRoom(domainId);

            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 4124 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds.Add(4124, roomId);

            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds);
            Assert.AreEqual(0, result.Facts.Count());
        }

        [TestMethod]
        public void CanPublishFacts()
        {
            FactTreeMemento postTree = new FactTreeMemento(0);
            postTree.Add(new IdentifiedFactMemento(
                new FactID { key = 3961 },
                CreateDomain()));
            postTree.Add(new IdentifiedFactMemento(
                new FactID { key = 4979 },
                CreateRoom(3961)));
            _service.Post("clientGuid1", "domain", postTree, new List<UnpublishMemento>());

            FactTreeMemento getTree = new FactTreeMemento(0);
            getTree.Add(new IdentifiedFactMemento(
                new FactID { key = 9898 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds[9898] = 0;
            var result = _service.GetMany("clientGuid2", "domain", getTree, pivotIds);

            Assert.AreEqual(2, result.Facts.Count());
            IdentifiedFactRemote  resultDomain = (IdentifiedFactRemote) result.Facts.ElementAt(0);
            IdentifiedFactMemento resultRoom   = (IdentifiedFactMemento)result.Facts.ElementAt(1);
            Assert.AreEqual(9898, resultDomain.RemoteId.key);
            Assert.AreEqual(TypeRoom, resultRoom.Memento.FactType);
            Assert.AreEqual(resultDomain.Id, resultRoom.Memento.Predecessors.ElementAt(0).ID);
        }

        [TestMethod]
        public void SkipsFactsToSourceClient()
        {
            FactTreeMemento postTree = new FactTreeMemento(0);
            postTree.Add(new IdentifiedFactMemento(
                new FactID { key = 3961 },
                CreateDomain()));
            postTree.Add(new IdentifiedFactMemento(
                new FactID { key = 4979 },
                CreateRoom(3961)));
            _service.Post("clientGuid1", "domain", postTree, new List<UnpublishMemento>());

            FactTreeMemento getTree = new FactTreeMemento(0);
            getTree.Add(new IdentifiedFactMemento(
                new FactID { key = 9898 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds[9898] = 0;
            var result = _service.GetMany("clientGuid1", "domain", getTree, pivotIds);

            Assert.AreEqual(0, result.Facts.Count());
        }
    }
}
