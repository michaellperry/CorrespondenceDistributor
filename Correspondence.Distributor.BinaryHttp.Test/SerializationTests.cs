using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;
using Client = UpdateControls.Correspondence.BinaryHTTPClient;
using Server = Correspondence.Distributor.BinaryHttp;

namespace Correspondence.Distributor.BinaryHttp.Test
{
    [TestClass]
    public class SerializationTests
    {
        [TestMethod]
        public void BinaryHttp_Serialization_GetManyRequest_EmptyTree()
        {
            Client.GetManyRequest clientRequest = new Client.GetManyRequest
            {
                ClientGuid = "{96E5C2E2-1345-482E-9668-6009DCDC0E70}",
                Domain = "TestDomain",
                TimeoutSeconds = 30,
                PivotTree = new FactTreeMemento(0),
                PivotIds = new List<Client.FactTimestamp>()
            };

            var serverRequest = WriteAndRead(
                w => clientRequest.Write(w),
                r => Server.BinaryRequest.Read(r) as Server.GetManyRequest);

            Assert.IsNotNull(serverRequest);
            Assert.AreEqual(Guid.Parse("{96E5C2E2-1345-482E-9668-6009DCDC0E70}"), serverRequest.ClientGuid);
            Assert.AreEqual("TestDomain", serverRequest.Domain);
            Assert.AreEqual(30, serverRequest.TimeoutSeconds);
            Assert.AreEqual(0, serverRequest.PivotTree.Facts.Count());
            Assert.AreEqual(0, serverRequest.PivotIds.Count());
        }

        [TestMethod]
        public void BinaryHttp_Serialization_GetManyRequest_SingleFact()
        {
            Client.GetManyRequest clientRequest = new Client.GetManyRequest
            {
                ClientGuid = "{96E5C2E2-1345-482E-9668-6009DCDC0E70}",
                Domain = "TestDomain",
                TimeoutSeconds = 30,
                PivotTree = CreateTreeWithSingleFact(),
                PivotIds = new List<Client.FactTimestamp>
                {
                    new Client.FactTimestamp { FactId = 42, TimestampId = 28768 }
                }
            };

            var serverRequest = WriteAndRead(
                w => clientRequest.Write(w),
                r => Server.BinaryRequest.Read(r) as Server.GetManyRequest);

            Assert.IsNotNull(serverRequest);
            Assert.AreEqual(1, serverRequest.PivotTree.Facts.Count());
            Assert.AreEqual(42, serverRequest.PivotTree.Facts.ElementAt(0).Id.key);
            IdentifiedFactMemento fact = serverRequest.PivotTree.Facts.ElementAt(0) as IdentifiedFactMemento;
            Assert.IsNotNull(fact);
            Assert.AreEqual("TestModel.Domain", fact.Memento.FactType.TypeName);
            Assert.AreEqual(1, serverRequest.PivotIds.Count());
            Assert.AreEqual(42, serverRequest.PivotIds.ElementAt(0).FactId);
            Assert.AreEqual(28768, serverRequest.PivotIds.ElementAt(0).TimestampId);
        }

        [TestMethod]
        public void BinaryHttp_Serialization_GetManyRequest_MultipleFacts()
        {
            Client.GetManyRequest clientRequest = new Client.GetManyRequest
            {
                ClientGuid = "{96E5C2E2-1345-482E-9668-6009DCDC0E70}",
                Domain = "TestDomain",
                TimeoutSeconds = 30,
                PivotTree = CreateTreeWithMultipleFacts(),
                PivotIds = new List<Client.FactTimestamp>
                {
                    new Client.FactTimestamp { FactId = 42, TimestampId = 28768 }
                }
            };

            var serverRequest = WriteAndRead(
                w => clientRequest.Write(w),
                r => Server.BinaryRequest.Read(r) as Server.GetManyRequest);

            Assert.IsNotNull(serverRequest);
            Assert.AreEqual(2, serverRequest.PivotTree.Facts.Count());
            Assert.AreEqual(42, serverRequest.PivotTree.Facts.ElementAt(0).Id.key);
            Assert.AreEqual(73, serverRequest.PivotTree.Facts.ElementAt(1).Id.key);
            IdentifiedFactMemento game = serverRequest.PivotTree.Facts.ElementAt(1) as IdentifiedFactMemento;
            Assert.IsNotNull(game);
            Assert.AreEqual("TestModel.Game", game.Memento.FactType.TypeName);
            Assert.AreEqual(1, game.Memento.Predecessors.Count());
            Assert.AreEqual(42, game.Memento.Predecessors.ElementAt(0).ID.key);
            Assert.AreEqual("TestModel.Game", game.Memento.Predecessors.ElementAt(0).Role.DeclaringType.TypeName);
            Assert.AreEqual("domain", game.Memento.Predecessors.ElementAt(0).Role.RoleName);
            Assert.IsTrue(game.Memento.Predecessors.ElementAt(0).IsPivot);
            Assert.AreEqual(4, game.Memento.Data.Length);
            Assert.AreEqual(1, game.Memento.Data[0]);
            Assert.AreEqual(2, game.Memento.Data[1]);
            Assert.AreEqual(3, game.Memento.Data[2]);
            Assert.AreEqual(4, game.Memento.Data[3]);
        }

        [TestMethod]
        public void BinaryHttp_Serialization_GetManyResponse_EmptyTree()
        {
            var serverResponse = new Server.GetManyResponse
            {
                FactTree = new FactTreeMemento(0),
                PivotIds = new List<FactTimestamp>()
            };

            var clientResponse = WriteAndRead(
                w => serverResponse.Write(w),
                r => Client.BinaryResponse.Read(r) as Client.GetManyResponse);

            Assert.IsNotNull(clientResponse);
            Assert.AreEqual(0, clientResponse.FactTree.Facts.Count());
            Assert.AreEqual(0, clientResponse.PivotIds.Count());
        }

        [TestMethod]
        public void BinaryHttp_Serialization_GetManyResponse_SingleFact()
        {
            var serverResponse = new Server.GetManyResponse
            {
                FactTree = CreateTreeWithSingleFact(),
                PivotIds = new List<FactTimestamp>
                {
                    new FactTimestamp { FactId = 42, TimestampId = 67676 }
                }
            };

            var clientResponse = WriteAndRead(
                w => serverResponse.Write(w),
                r => Client.BinaryResponse.Read(r) as Client.GetManyResponse);

            Assert.IsNotNull(clientResponse);
            Assert.AreEqual(1, clientResponse.FactTree.Facts.Count());
            Assert.AreEqual(42, clientResponse.FactTree.Facts.ElementAt(0).Id.key);
            IdentifiedFactMemento fact = clientResponse.FactTree.Facts.ElementAt(0) as IdentifiedFactMemento;
            Assert.IsNotNull(fact);
            Assert.AreEqual("TestModel.Domain", fact.Memento.FactType.TypeName);
            Assert.AreEqual(1, clientResponse.PivotIds.Count());
            Assert.AreEqual(42, clientResponse.PivotIds.ElementAt(0).FactId);
            Assert.AreEqual(67676, clientResponse.PivotIds.ElementAt(0).TimestampId);
        }

        [TestMethod]
        public void BinaryHttp_Serialization_GetManyResponse_MultipleFacts()
        {
            var serverResponse = new Server.GetManyResponse
            {
                FactTree = CreateTreeWithMultipleFacts(),
                PivotIds = new List<FactTimestamp>
                {
                    new FactTimestamp { FactId = 42, TimestampId = 67676 }
                }
            };

            var clientResponse = WriteAndRead(
                w => serverResponse.Write(w),
                r => Client.BinaryResponse.Read(r) as Client.GetManyResponse);

            Assert.IsNotNull(clientResponse);
            Assert.AreEqual(2, clientResponse.FactTree.Facts.Count());
            Assert.AreEqual(42, clientResponse.FactTree.Facts.ElementAt(0).Id.key);
            Assert.AreEqual(73, clientResponse.FactTree.Facts.ElementAt(1).Id.key);
            IdentifiedFactMemento game = clientResponse.FactTree.Facts.ElementAt(1) as IdentifiedFactMemento;
            Assert.IsNotNull(game);
            Assert.AreEqual("TestModel.Game", game.Memento.FactType.TypeName);
            Assert.AreEqual(1, game.Memento.Predecessors.Count());
            Assert.AreEqual(42, game.Memento.Predecessors.ElementAt(0).ID.key);
            Assert.AreEqual("TestModel.Game", game.Memento.Predecessors.ElementAt(0).Role.DeclaringType.TypeName);
            Assert.AreEqual("domain", game.Memento.Predecessors.ElementAt(0).Role.RoleName);
            Assert.IsTrue(game.Memento.Predecessors.ElementAt(0).IsPivot);
            Assert.AreEqual(4, game.Memento.Data.Length);
            Assert.AreEqual(1, game.Memento.Data[0]);
            Assert.AreEqual(2, game.Memento.Data[1]);
            Assert.AreEqual(3, game.Memento.Data[2]);
            Assert.AreEqual(4, game.Memento.Data[3]);
        }

        [TestMethod]
        public void BinaryHttp_Serialization_PostRequest()
        {
            var serverRequest = WriteAndRead(
                w => new Client.PostRequest
                {
                    ClientGuid = "{723B7C9F-7E80-4538-8CE5-EA5A73D13559}",
                    Domain = "Cheese",
                    MessageBody = CreateTreeWithSingleFact(),
                    UnpublishedMessages = new List<UnpublishMemento>
                    {
                        new UnpublishMemento(new FactID { key = 37 }, CreateRoleDomain())
                    }
                }.Write(w),
                r => Server.BinaryRequest.Read(r) as Server.PostRequest);

            Assert.IsNotNull(serverRequest);
            Assert.AreEqual(Guid.Parse("{723B7C9F-7E80-4538-8CE5-EA5A73D13559}"), serverRequest.ClientGuid);
            Assert.AreEqual("Cheese", serverRequest.Domain);
            Assert.AreEqual(1, serverRequest.MessageBody.Facts.Count());
            Assert.AreEqual(1, serverRequest.UnpublishedMessages.Count());
            Assert.AreEqual(37, serverRequest.UnpublishedMessages.ElementAt(0).MessageId.key);
            Assert.AreEqual("domain", serverRequest.UnpublishedMessages.ElementAt(0).Role.RoleName);
        }

        [TestMethod]
        public void BinaryHttp_Serialization_PostResponse()
        {
            var response = WriteAndRead(
                w => new Server.PostResponse().Write(w),
                r => Client.BinaryResponse.Read(r) as Client.PostResponse);

            Assert.IsNotNull(response);
        }

        private static T WriteAndRead<T>(Action<BinaryWriter> write, Func<BinaryReader, T> read)
        {
            byte[] buffer = WriteToBuffer(write);
            return ReadFromBuffer(buffer, read);
        }

        private static byte[] WriteToBuffer(Action<BinaryWriter> write)
        {
            MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                write(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static T ReadFromBuffer<T>(byte[] buffer, Func<BinaryReader, T> read)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
            {
                return read(reader);
            }
        }

        private static FactTreeMemento CreateTreeWithSingleFact()
        {
            FactTreeMemento tree = new FactTreeMemento(0);
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 42 },
                new FactMemento(new CorrespondenceFactType("TestModel.Domain", 1))));
            return tree;
        }

        private static FactTreeMemento CreateTreeWithMultipleFacts()
        {
            FactTreeMemento tree = CreateTreeWithSingleFact();
            var game = new FactMemento(
                new CorrespondenceFactType("TestModel.Game", 1))
                .AddPredecessor(
                    CreateRoleDomain(),
                    new FactID { key = 42 },
                    true);
            game.Data = new byte[] { 1, 2, 3, 4 };
            tree.Add(new IdentifiedFactMemento(
                new FactID { key = 73 },
                game));
            return tree;
        }
        private static RoleMemento CreateRoleDomain()
        {
            return new RoleMemento(
                new CorrespondenceFactType("TestModel.Game", 1),
                "domain",
                new CorrespondenceFactType("TestModel.Domain", 1),
                true);
        }
    }
}
