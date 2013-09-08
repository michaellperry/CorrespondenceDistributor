using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    public class AncestorMessage
    {
        public FactID AncestorFactId { get; set; }
        public int AncestorRoleId { get; set; }
        public MessageMemento Message { get; set; }
    }
}
