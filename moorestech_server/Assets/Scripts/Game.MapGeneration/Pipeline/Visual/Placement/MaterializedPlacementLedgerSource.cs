namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     生成直後の先焼きのように、既に手元にある台帳をそのまま差し出す口
    ///     The port for a ledger already at hand, as the prebake right after generation has
    /// </summary>
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
