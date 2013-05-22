using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;
using System.Collections.Generic;
using System.Linq;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class DistributorServiceTests
    {
        private static readonly CorrespondenceFactType TypeDomain =
            new CorrespondenceFactType("TestModel.Domain", 1);
        private static readonly CorrespondenceFactType TypeRoom =
            new CorrespondenceFactType("TestModel.Room", 1);
        private static readonly RoleMemento RoleRoomDomain =
            new RoleMemento(TypeRoom, "domain", TypeDomain, true);

        private DistributorService _service;
        private MockRepository _mockRepository;

        [TestInitialize]
        public void Initialize()
        {
            _mockRepository = new MockRepository();
            _service = new DistributorService(_mockRepository);
        }

        [TestMethod]
        public void CanGetWithNoPivots()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds, 0);
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

            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds, 0);
            Assert.AreEqual(2, result.Facts.Count());
            IdentifiedFactMemento resultDomain = (IdentifiedFactMemento)result.Facts.ElementAt(0);
            IdentifiedFactMemento resultRoom   = (IdentifiedFactMemento)result.Facts.ElementAt(1);
            Assert.AreEqual(TypeDomain, resultDomain.Memento.FactType);
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
                new FactMemento(TypeDomain)));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds.Add(4124, roomId);

            var result = _service.GetMany("clientGuid", "domain", tree, pivotIds, 0);
            Assert.AreEqual(0, result.Facts.Count());
        }

        private long AddDomain()
        {
            return _mockRepository.AddFact(new FactMemento(TypeDomain));
        }

        private long AddRoom(long domainId)
        {
            FactMemento room = new FactMemento(TypeRoom);
            room.Data = new byte[] { 1, 2, 3, 4 };
            room.AddPredecessor(RoleRoomDomain, new FactID { key = domainId }, true);
            return _mockRepository.AddFact(room);
        }
    }
}
