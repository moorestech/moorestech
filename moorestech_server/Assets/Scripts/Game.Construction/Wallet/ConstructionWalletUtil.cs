using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 財布キー解決と残り設置数の算術。坂ベルトは直線代表、分岐器と他は自身
    /// Resolves the wallet key for remaining placements (belt slopes normalize to the straight block, splitters and others are themselves) and owns every arithmetic rule about the remainder
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeBlockId(blockId);
        }

        // 財布を通すブロックか（1セット1個は素通り）
        // Whether the block goes through the wallet at all (one placement per set bypasses it)
        public static bool UsesWallet(int placementsPerCost)
        {
            return 1 < placementsPerCost;
        }

        // 残りが1つでもあれば素材を払わず設置できる
        // A non-empty wallet covers the placement without paying materials
        public static bool IsCoveredByWallet(int remaining)
        {
            return 0 < remaining;
        }

        // 撤去+1がNに達するか（達すれば凝縮し財布0へ）
        // Whether returning one reaches placementsPerCost (condenses and resets wallet to zero)
        public static bool WouldCondense(int remaining, int placementsPerCost)
        {
            return placementsPerCost <= remaining + 1;
        }

        // 撤去返却後の残り。Nに達した分は素材へ凝縮し財布は空になる
        // The remainder after a removal's return; the portion that reached one set's worth condenses into materials and empties the wallet
        public static int AdvanceOnRemoval(int remaining, bool condensed)
        {
            return condensed ? 0 : remaining + 1;
        }

        // 設置後の残り。素材を払ったセルは1セット分を補充してから1消費する（残り=N-1）
        // The remainder after a placement; a cell that paid materials refills one set's worth and then consumes one (remaining = N-1)
        public static int AdvanceOnPlacement(int remaining, int placementsPerCost, bool coveredByWallet)
        {
            return coveredByWallet ? remaining - 1 : remaining + placementsPerCost - 1;
        }

        // 置くセル数のうち実際に払うコストセット数。残りで賄える分は払わない
        // Cost sets actually paid for the given cells; what the remainder covers is not paid
        public static int CalculateRequiredCostSets(int remaining, int cellCount, int placementsPerCost)
        {
            if (!UsesWallet(placementsPerCost)) return cellCount;

            var payableCells = cellCount - remaining;
            if (payableCells <= 0) return 0;
            return (payableCells + placementsPerCost - 1) / placementsPerCost;
        }

        // 財布の残りと払えるセット数から置ける数を出す。大量所持のオーバーフローを避ける
        // Placeable count from the wallet remainder plus the affordable cost sets, guarding against overflow on very large holdings
        public static int CalculatePlaceableCount(int remaining, int affordableCostSets, int placementsPerCost)
        {
            if (!UsesWallet(placementsPerCost) || affordableCostSets == int.MaxValue) return affordableCostSets;

            var total = remaining + (long)affordableCostSets * placementsPerCost;
            return int.MaxValue < total ? int.MaxValue : (int)total;
        }
    }
}
