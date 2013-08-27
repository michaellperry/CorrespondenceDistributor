using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;
using System.Collections.Generic;

namespace Correspondence.Distributor.SqlRepository.Test
{
    [TestClass]
    [DeploymentItem("App.config")]
    public class RepositoryTests
    {
        private const string TestDomain = "TestDomain";
        private static readonly Guid TestClient = Guid.Parse("{23EBD408-FDB3-402A-B10A-439240971BA8}");
        private static readonly Guid AnotherClient = Guid.Parse("{BF2C8CD0-57F0-4E5B-9DE6-E75CF4C23017}");

        private static readonly CorrespondenceFactType GameType =
            new CorrespondenceFactType("Test.Model.Game", 1);
        private static readonly CorrespondenceFactType MoveType =
            new CorrespondenceFactType("Test.Model.Move", 1);
        private static readonly RoleMemento MoveGame =
            new RoleMemento(MoveType, "game", GameType, true);

        Repository _repository;

        [TestInitialize]
        public void Initialize()
        {
            _repository = new Repository("Correspondence")
                .UpgradeDatabase();
        }

        [TestMethod]
        public void NewFactIsNotFound()
        {
            Guid gameGuid = Guid.NewGuid();

            var findGame = NewGame(gameGuid);
            FactID? findId = _repository.FindExistingFact(TestDomain, findGame);

            Assert.IsNull(findId);
        }

        [TestMethod]
        public void CanSaveAndFindAFact()
        {
            Guid gameGuid = Guid.NewGuid();

            var saveGame = NewGame(gameGuid);
            FactID saveId = _repository.Save(TestDomain, saveGame, TestClient);

            var findGame = NewGame(gameGuid);
            FactID? findId = _repository.FindExistingFact(TestDomain, findGame);

            Assert.IsNotNull(findId);
            Assert.AreEqual(saveId, findId);
        }

        [TestMethod]
        public void CanSaveAndFindAFactWithPredecessor()
        {
            Guid gameGuid = Guid.NewGuid();
            var game = NewGame(gameGuid);
            FactID gameId = _repository.Save(TestDomain, game, TestClient);

            var saveMove = NewMove(gameId, 1);
            FactID saveId = _repository.Save(TestDomain, saveMove, TestClient);

            var findMove = NewMove(gameId, 1);
            FactID? findId = _repository.FindExistingFact(TestDomain, findMove);

            Assert.IsNotNull(findId);
            Assert.AreEqual(saveId, findId);
        }

        [TestMethod]
        public void FactIsPublishedToPredecessor()
        {
            Guid gameGuid = Guid.NewGuid();
            var game = NewGame(gameGuid);
            FactID gameId = _repository.Save(TestDomain, game, TestClient);

            var saveMove = NewMove(gameId, 1);
            FactID saveId = _repository.Save(TestDomain, saveMove, TestClient);

            List<FactID> messages = _repository.LoadRecentMessages(
                TestDomain, gameId, AnotherClient, new TimestampID(0, 0));

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(saveId, messages[0]);
        }

        [TestMethod]
        public void SkipMessagesOriginatingFromThisClient()
        {
            Guid gameGuid = Guid.NewGuid();
            var game = NewGame(gameGuid);
            FactID gameId = _repository.Save(TestDomain, game, TestClient);

            var saveMove = NewMove(gameId, 1);
            FactID saveId = _repository.Save(TestDomain, saveMove, TestClient);

            List<FactID> messages = _repository.LoadRecentMessages(
                TestDomain, gameId, TestClient, new TimestampID(0, 0));

            Assert.AreEqual(0, messages.Count);
        }

        private static FactMemento NewGame(Guid gameGuid)
        {
            FactMemento saveGame = new FactMemento(GameType);
            saveGame.Data = gameGuid.ToByteArray();
            return saveGame;
        }

        private static FactMemento NewMove(FactID gameId, byte moveIndex)
        {
            var move = new FactMemento(MoveType);
            move.Data = new byte[] { moveIndex };
            move.AddPredecessor(MoveGame, gameId, true);
            return move;
        }
    }
}
