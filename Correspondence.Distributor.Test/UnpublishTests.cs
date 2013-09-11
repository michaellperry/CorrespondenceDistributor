using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class UnpublishTests : TestBase
    {
        private static readonly Guid ClientGuid1 = Guid.Parse("{23EBD408-FDB3-402A-B10A-439240971BA8}");
        private static readonly Guid ClientGuid2 = Guid.Parse("{BF2C8CD0-57F0-4E5B-9DE6-E75CF4C23017}");

        private DistributorService _service;

        [TestInitialize]
        public void Initialize()
        {
            _service = new DistributorService(_mockRepository, null);
        }

        [TestMethod]
        public async Task CanUnpublishFacts()
        {
            // There is a room published to the domain.
            long domainId = AddDomain();
            long roomId = AddRoom(domainId);

            // Client 1 unpublishes the room.
            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 3333 },
                CreateDomain()));
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 9879 },
                CreateRoom(3333)));
            _service.Post(ClientGuid1, "domain", tree,
                new List<UnpublishMemento>()
                {
                    new UnpublishMemento(new FactID { key = 9879 }, RoleRoomDomain)
                });

            // Client 2 subscribes to the domain.
            FactTreeMemento pivots = new FactTreeMemento(0);
            pivots.Add(new IdentifiedFactMemento(
                new FactID { key = 2222 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>()
            {
                { 2222, 0 }
            };
            var result = await _service.GetManyAsync(ClientGuid2, "domain", pivots, pivotIds, 0);

            // The room is conspicuously absent.
            Assert.AreEqual(0, result.Tree.Facts.Count());
        }
    }
}
