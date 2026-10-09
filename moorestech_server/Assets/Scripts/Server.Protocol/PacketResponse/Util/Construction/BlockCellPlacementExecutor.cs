using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    public class BlockCellPlacementExecutor
    {
        private readonly ConstructionWalletService _wallet;

        public BlockCellPlacementExecutor(ConstructionWalletService wallet)
        {
            _wallet = wallet;
        }

        // 財布の計画と所持素材から支払い可否を決める
        // Decide affordability from the wallet plan and held materials
        public BlockCellPlacement PlanCell(BlockId blockId, int playerId, IOpenableInventory inventory, bool isPaymentWaived)
        {
            var walletPlan = _wallet.PlanPlacement(blockId, playerId);
            var isAffordable = isPaymentWaived || ConstructionCostService.HasRequiredItems(walletPlan.ItemsToConsume, inventory.InventoryItems);
            return new BlockCellPlacement(walletPlan, isPaymentWaived, isAffordable);
        }

        // 追加成功時だけ支払いを確定し、無料設置では支払者も記録しない
        // Commit payment only after adding succeeds; waived placement also keeps no payer record
        public bool TryPlaceCell(BlockCellPlacement placement, BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, IOpenableInventory inventory, out IBlock block)
        {
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, createParams, out block))
            {
                Debug.LogWarning($"[BlockCellPlacement] TryAddBlock failed: pos={position} block={blockId}");
                return false;
            }
            if (!placement.IsPaymentWaived) _wallet.CommitPlacement(placement.WalletPlan, inventory, block.BlockInstanceId);
            return true;
        }

        // 列設置の財布変更通知を操作末尾で集約する
        // Aggregate wallet change notifications at the end of a placement run
        public void FlushRemainingCountChanges()
        {
            _wallet.FlushRemainingCountChanges();
        }
    }
}
