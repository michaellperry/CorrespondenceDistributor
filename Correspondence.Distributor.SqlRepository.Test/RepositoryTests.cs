using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository.Test
{
    [TestClass]
    [DeploymentItem("App.config")]
    public class RepositoryTests
    {
        CorrespondenceFactType GameType = new CorrespondenceFactType("Test.Model.Game", 1);

        Repository _repository;

        [TestInitialize]
        public void Initialize()
        {
            _repository = new Repository("Correspondence")
                .UpgradeDatabase();
        }

        [TestMethod]
        public void CanSaveAFact()
        {
            FactMemento game = new FactMemento(GameType);
            game.Data = new byte[] { 0x53, 0x30, 0x82, 0xF1 };
            FactID id = _repository.Save("TestDomain", game, "TestClient");

            Assert.AreEqual(1, id.key);
        }
    }
}
