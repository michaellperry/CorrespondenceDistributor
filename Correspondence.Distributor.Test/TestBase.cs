using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Test
{
    public class TestBase
    {
        protected static readonly CorrespondenceFactType TypeDomain =
            new CorrespondenceFactType("TestModel.Domain", 1);
        protected static readonly CorrespondenceFactType TypeRoom =
            new CorrespondenceFactType("TestModel.Room", 1);
        protected static readonly RoleMemento RoleRoomDomain =
            new RoleMemento(TypeRoom, "domain", TypeDomain, true);

        protected MockRepository _mockRepository;

        [TestInitialize]
        public void InitializeMockRepository()
        {
            _mockRepository = new MockRepository();
        }

        protected long AddDomain()
        {
            return _mockRepository.Save("domain", CreateDomain(), Guid.Empty).key;
        }

        protected static FactMemento CreateDomain()
        {
            return new FactMemento(TypeDomain);
        }

        protected long AddRoom(long domainId)
        {
            return _mockRepository.Save("domain", CreateRoom(domainId), Guid.Empty).key;
        }

        protected static FactMemento CreateRoom(long domainId)
        {
            FactMemento room = new FactMemento(TypeRoom);
            room.Data = new byte[] { 1, 2, 3, 4 };
            room.AddPredecessor(RoleRoomDomain, new FactID { key = domainId }, true);
            return room;
        }
    }
}
