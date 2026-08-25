using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 財布キー解決と残り設置数の算術。ベルトは直線代表、他は自身
    /// Resolves the wallet key for remaining placements (belt families normalize to the straight block, others are themselves) and owns every arithmetic rule about the remainder
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) ? family.StraightBlockId : blockId;
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
