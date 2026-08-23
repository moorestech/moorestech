namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    public class MaterializedPlacementLedgerSource : IPlacementLedgerSource
    {
        private readonly PlacementLedger _ledger;

        public MaterializedPlacementLedgerSource(PlacementLedger ledger)
        {
            _ledger = ledger;
        }

        public PlacementLedger Resolve()
        {
            return _ledger;
        }
    }
}
