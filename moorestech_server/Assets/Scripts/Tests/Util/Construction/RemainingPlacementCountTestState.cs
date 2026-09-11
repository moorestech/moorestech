using Core.Master;
using Game.Construction;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Util
{
    /// <summary>
    /// テストが望む残り設置数を、プロダクションの遷移だけで組み立てるヘルパ
    /// Builds the remaining placement count a test wants, using nothing but the production transitions
    /// </summary>
    public static class RemainingPlacementCountTestState
    {
        public static void SetRemainingCount(ServiceProvider serviceProvider, int playerId, BlockId walletBlockId, int remainingCount)
        {
            var mutation = serviceProvider.GetService<IRemainingPlacementCountMutation>();
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(walletBlockId).PlacementsPerCost;

            // 増やす側は撤去返却(+1)、減らす側は財布で賄う設置(-1)を目標まで回す
            // Growing uses a removal's return (+1) and shrinking a wallet-covered placement (-1), repeated up to the target
            var current = lookup.GetRemainingCount(playerId, walletBlockId);
            while (current < remainingCount)
            {
                mutation.ApplyReturn(playerId, walletBlockId, false);
                current++;
            }

            while (remainingCount < current)
            {
                mutation.ApplyPlacement(playerId, walletBlockId, placementsPerCost, ConstructionWalletUsage.CoveredByWallet);
                current--;
            }
        }
    }
}
