using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // pass-1からpass-2へ渡す配置台帳
    // The ledger carried from pass-1 (placement) to pass-2 (visuals)
    public class PlacementLedger
    {
        private readonly List<LedgerPlacement> _placements = new();
        public IReadOnlyList<LedgerPlacement> Placements => _placements;

        public void Add(LedgerPlacement placement)
        {
            _placements.Add(placement);
        }
    }
}
