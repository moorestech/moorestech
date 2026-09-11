using System;
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

            // 財布が取り得ない残数を黙って素通しすると、テストが成立しない前提のまま緑になる
            // Silently passing a remainder the wallet can never hold would leave a test green on a premise that never held
            if (remainingCount < 0 || placementsPerCost <= remainingCount) throw new ArgumentOutOfRangeException(nameof(remainingCount), remainingCount, $"wallet of {walletBlockId} holds 0 to {placementsPerCost - 1}");

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
