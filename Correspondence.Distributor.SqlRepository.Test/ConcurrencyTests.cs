using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository.Test
{
    [TestClass]
    public class ConcurrencyTests
    {
        private const string TestDomain = "TestDomain";
        private static readonly Guid TestClient = Guid.Parse("{23EBD408-FDB3-402A-B10A-439240971BA8}");

        private static readonly CorrespondenceFactType GameType =
            new CorrespondenceFactType("Test.Model.Game", 1);

        Repository _repository;

        [TestInitialize]
        public void Initialize()
        {
            _repository = new Repository("Correspondence")
                .UpgradeDatabase();
        }

        [TestMethod]
        public async Task CanWriteAFactConcurrently()
        {
            Guid gameId = Guid.NewGuid();
            var game1 = NewGame(gameId);
            var game2 = NewGame(gameId);

            _repository.Retried = false;
            var task1 = Task.Run(() => _repository.Save(TestDomain, game1, TestClient));
            var task2 = Task.Run(() => _repository.Save(TestDomain, game2, TestClient));
            await Task.WhenAll(task1, task2);
            var id1 = task1.Result;
            var id2 = task2.Result;

            Assert.AreEqual(id1, id2);
            Assert.IsTrue(_repository.Retried);
        }

        [TestMethod]
        public async Task CanWriteDifferentFactsConcurrentlyWithoutRetry()
        {
            var game1 = NewGame(Guid.NewGuid());
            var game2 = NewGame(Guid.NewGuid());

            _repository.Retried = false;
            var task1 = Task.Run(() => _repository.Save(TestDomain, game1, TestClient));
            var task2 = Task.Run(() => _repository.Save(TestDomain, game2, TestClient));
            await Task.WhenAll(task1, task2);
            var id1 = task1.Result;
            var id2 = task2.Result;

            Assert.AreNotEqual(id1, id2);
            Assert.IsFalse(_repository.Retried);
        }

        private static FactMemento NewGame(Guid gameGuid)
        {
            FactMemento game = new FactMemento(GameType);
            game.Data = gameGuid.ToByteArray();
            return game;
        }
    }
}
