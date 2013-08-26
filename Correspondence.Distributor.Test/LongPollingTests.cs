using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class LongPollingTests : TestBase
    {
        private DistributorService _service;

        [TestInitialize]
        public void Initialize()
        {
            _service = new DistributorService(_mockRepository);
        }

        [TestMethod]
        public void DelaysForPollingInterval()
        {
            Task<GetManyResult> task = GetFromClient1();

            Assert.IsFalse(task.IsCompleted);

            GetManyResult result = Slow(task);
            Assert.AreEqual(0, result.Tree.Facts.Count());
        }

        [TestMethod]
        public void ContinuesWhenFactIsPublished()
        {
            Task<GetManyResult> task = GetFromClient1();
            PostFromClient2();

            GetManyResult result = Fast(task);
            Assert.AreEqual(2, result.Tree.Facts.Count());
        }

        [TestMethod]
        public void CanInterruptServerFromSameClient()
        {
            Task<GetManyResult> task = GetFromClient1();

            _service.Interrupt("clientGuid1", "domain");

            GetManyResult result = Fast(task);
            Assert.AreEqual(0, result.Tree.Facts.Count());
        }

        private Task<GetManyResult> GetFromClient1()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 4124 },
                CreateDomain()));
            Dictionary<long, long> pivotIds = new Dictionary<long, long>();
            pivotIds[4124] = 0;
            _service.Post("clientGuid1", "domain", tree, new List<UnpublishMemento>());
            return _service.GetManyAsync("clientGuid1", "domain", tree, pivotIds, 1);
        }

        private void PostFromClient2()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 12 },
                CreateDomain()));
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 13 },
                CreateRoom(12)));
            _service.Post("clientGuid2", "domain", tree, new List<UnpublishMemento>());
        }

        private static GetManyResult Slow(Task<GetManyResult> task)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = task.Result;
            stopwatch.Stop();
            var seconds = stopwatch.Elapsed.TotalSeconds;
            Assert.IsTrue(0.95 < seconds && seconds < 1.05);
            return result;
        }

        private static GetManyResult Fast(Task<GetManyResult> task)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = task.Result;
            stopwatch.Stop();
            var seconds = stopwatch.Elapsed.TotalSeconds;
            Assert.IsTrue(seconds < 0.05);
            return result;
        }
    }
}