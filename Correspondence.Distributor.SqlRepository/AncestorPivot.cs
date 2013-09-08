using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    public class AncestorPivot
    {
        public FactID AncestorFactId { get; set; }
        public int AncestorRoleId { get; set; }
        public FactID PivotId { get; set; }
    }
}
