using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // pass-1（配置）から pass-2（見た目）へ渡す台帳。格子全体・タイル順に積まれる
    // The ledger carried from pass-1 (placement) to pass-2 (visuals), accumulated grid-wide in tile order
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
