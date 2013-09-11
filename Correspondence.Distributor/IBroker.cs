using System.Collections.Generic;
using System.Threading.Tasks;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor
{
    public interface IBroker
    {
        Task<List<string>> SendPushNotifications(FactTreeMemento factTree, IEnumerable<string> subscriberDeviceUris);
    }
}
