using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;
using System.Collections.Generic;
using Correspondence.Distributor;
using System.Threading.Tasks;
using System.Linq;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class LongPollingTests : TestBase
    {
        private DistributorNode _node;

        [TestInitialize]
        public void Initialize()
        {
            var service = new DistributorService(_mockRepository);
            _node = new DistributorNode(service);
        }

        [TestMethod]
        public void DelaysForPollingInterval()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 4124 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds[4124] = 0;
            Task<GetManyResult> result = _node.GetMany("clientGuid", "domain", tree, pivotIds, 1);

            Assert.IsFalse(result.IsCompleted);

            // Wait for 1 second.
            var gmResult = result.Result;
            Assert.AreEqual(0, gmResult.Tree.Facts.Count());
        }

        [TestMethod]
        public void ContinuesWhenFactIsPublished()
        {
            FactTreeMemento tree1 = new FactTreeMemento(0);
            tree1.Add(new IdentifiedFactMemento(
                new FactID { key = 4124 },
                CreateDomain()));
            Dictionary<long, long> pivotIds1 = new Dictionary<long, long>();
            pivotIds1[4124] = 0;
            _node.Post("clientGuid1", "domain", tree1, new List<UnpublishMemento>());
            Task<GetManyResult> result1 = _node.GetMany("clientGuid1", "domain", tree1, pivotIds1, 30);

            FactTreeMemento tree2 = new FactTreeMemento(0);
            tree2.Add(new IdentifiedFactMemento(
                new FactID { key = 12 },
                CreateDomain()));
            tree2.Add(new IdentifiedFactMemento(
                new FactID { key = 13 },
                CreateRoom(12)));
            _node.Post("clientGuid2", "domain", tree2, new List<UnpublishMemento>());

            var result = result1.Result;
            Assert.AreEqual(2, result.Tree.Facts.Count());
        }
    }
}