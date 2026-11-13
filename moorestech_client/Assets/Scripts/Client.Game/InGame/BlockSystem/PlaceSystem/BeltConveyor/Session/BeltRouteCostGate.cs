using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     経路全体の建設コストを財布と在庫で賄えるか。足りなければ全セルを赤にして不足を積み、置かない
    ///     Whether the wallet and inventory cover the whole route's construction cost; otherwise every cell turns red, the shortage is pushed, and nothing is placed
    /// </summary>
    public static class BeltRouteCostGate
    {
        public static bool Apply(List<PlaceInfo> infos, ConstructionWalletQuery wallet, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return true;

            var countsByBlock = new Dictionary<BlockId, int>();
            foreach (var info in infos)
            {
                countsByBlock.TryGetValue(info.BlockId, out var count);
                countsByBlock[info.BlockId] = count + 1;
            }

            // 分岐器とベルトのように種類が混ざるので、種類ごとに必要セット数を財布へ問い合わせる
            // Kinds mix (splitter plus belts), so each kind asks the wallet for its required cost sets
            var affordable = true;
            foreach (var (blockId, count) in countsByBlock)
            {
                var requiredCostSets = wallet.GetRequiredCostSets(blockId, count);
                if (requiredCostSets == 0) continue;
                var shortages = ConstructionCostShortageCalculator.Calculate(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems, requiredCostSets, inventoryItems);
                if (shortages.Count == 0) continue;
                feedback.AddMaterialShortages(shortages);
                affordable = false;
            }

            if (affordable) return true;
            foreach (var info in infos) info.Placeable = false;
            return false;
        }
    }
}
