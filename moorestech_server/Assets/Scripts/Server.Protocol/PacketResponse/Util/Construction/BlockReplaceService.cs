using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// ベルト系ブロックを搬送品ごと引き継いでリプレース設置する
    /// Replaces belt-family blocks in place while carrying over their in-transit items
    /// </summary>
    public class BlockReplaceService
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public BlockReplaceService(IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
        }

        // ベルトファミリーは直線ブロックのunlock状態を参照する（PlaceBlockProtocolと共通）
        // Belt families resolve unlock state through their straight block (shared with PlaceBlockProtocol)
        public static bool IsUnlocked(BlockId blockId, Guid blockGuid, IGameUnlockStateDataController unlockController)
        {
            var unlockGuid = BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family)
                ? MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId).BlockGuid
                : blockGuid;
            return unlockController.BlockUnlockStateInfos[unlockGuid].IsUnlocked;
        }

        public bool TryReplaceBlock(PlaceInfoMessagePack placeInfo, IOpenableInventory playerInventory, bool isFreePlacement)
        {
            var pos = placeInfo.Position;
            var newBlockId = placeInfo.BlockId;

            // Step1: 対象セルに既存ブロックがあり、新旧ともにリプレースファミリーであること
            // Step1: an existing block occupies the cell and both old and new are replace-family
            var oldBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            if (oldBlock == null) return false;
            if (!BlockReplaceFamilyUtil.IsReplaceFamily(oldBlock.BlockId)) return false;
            if (!BlockReplaceFamilyUtil.IsReplaceFamily(newBlockId)) return false;

            // Step2: 同一BlockId・同一Directionは冪等no-op（サーバー側防御）
            // Step2: identical block id and direction is an idempotent no-op (server-side guard)
            if (oldBlock.BlockId == newBlockId && oldBlock.BlockPositionInfo.BlockDirection == placeInfo.Direction) return true;

            // Step3: 新ブロックのunlock検証
            // Step3: validate the new block's unlock state
            var newBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(newBlockId);
            if (!IsUnlocked(newBlockId, newBlockMaster.BlockGuid, _gameUnlockStateDataController)) return false;

            // Step4-5: 搬送品と旧建設コスト返却を収集
            // Step4-5: collect in-transit items and the old construction-cost refund
            var transitItems = CollectTransitItems(oldBlock);
            var oldRefund = CreateConstructionRefund(oldBlock.BlockId);

            // Step6: 事前検証（無料設置時はコスト検証・消費・返却を全スキップ）
            // Step6: pre-validation (skip all cost validation/consumption/refund when free placement)
            var newCostItemCounts = ConstructionCostService.ToItemCounts(newBlockMaster.RequiredItems);
            if (!isFreePlacement && !PreValidate(playerInventory, oldRefund, transitItems, newCostItemCounts)) return false;

            // 旧ブロックを破棄する前に、分岐器のユーザー設定を新規作成パラメータへ退避する
            // Capture filter settings into creation parameters before destroying the old block
            var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToList();
            AddInheritedBlueprintSettings(oldBlock, newBlockMaster, createParams);

            // Step7: 旧ブロック撤去→旧コスト返却挿入
            // Step7: remove the old block, then insert the old construction refund
            ServerContext.WorldBlockDatastore.RemoveBlock(pos, BlockRemoveReason.Replace);
            if (!isFreePlacement) playerInventory.InsertItem(oldRefund);

            // Step8: 新ブロック設置（失敗時は搬送品をプレイヤーへ返しベストエフォート終了）
            // Step8: place the new block (on failure, return transit items to the player and end best-effort)
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(newBlockId, pos, placeInfo.Direction, createParams.ToArray(), out var newBlock))
            {
                InsertTransitToPlayer(transitItems, playerInventory);
                return false;
            }

            // Step9: 新コスト消費
            // Step9: consume the new construction cost
            if (!isFreePlacement) ConstructionCostService.ConsumeRequiredItems(newCostItemCounts, playerInventory);

            // Step10: 搬送品を新ベルトへ引き継ぎ、入らない分はプレイヤーへ
            // Step10: hand transit items over to the new belt; overflow goes to the player
            HandoverTransitItems(newBlock, transitItems, playerInventory);
            return true;

            #region Internal

            List<(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)> CollectTransitItems(IBlock block)
            {
                var result = new List<(ItemId, ItemInstanceId, double)>();

                // ベルト系は進行率付きで収集する
                // Belt-type blocks are collected with their progress rate
                if (block.ComponentManager.TryGetComponent<IItemCollectableBeltConveyor>(out var belt))
                {
                    foreach (var item in belt.BeltConveyorItems)
                    {
                        if (item == null) continue;
                        var rate = item.TotalTicks == 0 || item.TotalTicks == uint.MaxValue
                            ? 1.0
                            : item.RemainingTicks / (double)item.TotalTicks;
                        result.Add((item.ItemId, item.ItemInstanceId, rate));
                    }
                    return result;
                }

                // FilterSplitter等は全スロットをrate=1.0（入口）として収集
                // Non-belt blocks (FilterSplitter etc.) collect all slots at rate 1.0 (entry)
                if (block.ComponentManager.TryGetComponent<IBlockInventory>(out var inventory))
                {
                    for (var i = 0; i < inventory.GetSlotSize(); i++)
                    {
                        var stack = inventory.GetItem(i);
                        if (stack.Id == ItemMaster.EmptyItemId) continue;
                        for (var c = 0; c < stack.Count; c++) result.Add((stack.Id, stack.ItemInstanceId, 1.0));
                    }
                }
                return result;
            }

            List<IItemStack> CreateConstructionRefund(BlockId blockId)
            {
                // requiredItems定義ブロックのみ建設コストを全額返却（搬送品は別収集済み）
                // Refund the full construction cost only for requiredItems-defined blocks (transit collected separately)
                var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (master.RequiredItems == null || master.RequiredItems.Length == 0) return new List<IItemStack>();
                return ConstructionCostService.CreateRefundItems(ConstructionCostService.ToItemCounts(master.RequiredItems));
            }

            void AddInheritedBlueprintSettings(IBlock oldBlock, BlockMasterElement newBlockMaster, List<BlockCreateParam> createParams)
            {
                // 分岐器同士の置換では、ユーザー設定済みのフィルタ状態を新ブロックへ引き継ぐ
                // Carry user-configured filter settings into a replacement filter splitter
                if (newBlockMaster.BlockType != BlockMasterElement.BlockTypeConst.FilterSplitter) return;
                if (!oldBlock.ComponentManager.TryGetComponent<IBlockBlueprintSettings>(out var settings)) return;

                foreach (var param in createParams)
                {
                    if (param.Key == settings.BlueprintSettingsKey) return;
                }
                createParams.Add(new BlockCreateParam(settings.BlueprintSettingsKey, Encoding.UTF8.GetBytes(settings.GetBlueprintSettingsJson())));
            }

            bool PreValidate(IOpenableInventory inventory, List<IItemStack> refund, List<(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)> transit, (ItemId itemId, int count)[] newCost)
            {
                // 最悪ケース（旧コスト返却＋搬送品全部がプレイヤー行き）で溢れないこと
                // The worst case (old refund plus all transit items going to the player) must not overflow
                var worstCase = new List<IItemStack>(refund);
                var grouped = new Dictionary<ItemId, int>();
                foreach (var (itemId, _, _) in transit)
                {
                    grouped.TryGetValue(itemId, out var count);
                    grouped[itemId] = count + 1;
                }
                foreach (var pair in grouped) worstCase.Add(ServerContext.ItemStackFactory.Create(pair.Key, pair.Value));
                if (!inventory.InsertionCheck(worstCase)) return false;

                // 新コストを「現在インベントリ＋旧コスト返却」で賄えること
                // The new cost must be covered by the current inventory plus the old refund
                var available = new List<IItemStack>(inventory.InventoryItems);
                available.AddRange(refund);
                return ConstructionCostService.HasRequiredItems(newCost, available);
            }

            void HandoverTransitItems(IBlock block, List<(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)> transit, IOpenableInventory inventory)
            {
                // ベルトは進行率を維持し、その他の搬送系ブロックは通常の受入経路で復元する
                // Preserve progress for belts; restore other conveyor-family blocks through normal insertion
                var hasBelt = block.ComponentManager.TryGetComponent<VanillaBeltConveyorComponent>(out var belt);
                var hasBlockInventory = block.ComponentManager.TryGetComponent<IBlockInventory>(out var blockInventory);
                var overflow = new List<IItemStack>();
                foreach (var (itemId, itemInstanceId, remainingRate) in transit)
                {
                    if (hasBelt && belt.TryInsertItemWithRemainingRate(itemId, itemInstanceId, remainingRate)) continue;
                    if (hasBlockInventory)
                    {
                        var remaining = blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1, itemInstanceId), InsertItemContext.Empty);
                        if (remaining.Count == 0) continue;
                        if (TryRestoreIntoEmptySlot(blockInventory, remaining)) continue;
                    }
                    overflow.Add(ServerContext.ItemStackFactory.Create(itemId, 1, itemInstanceId));
                }
                if (overflow.Count != 0) inventory.InsertItem(overflow);
            }

            bool TryRestoreIntoEmptySlot(IBlockInventory blockInventory, IItemStack item)
            {
                // 接続未確立の分岐器でも、退避済みバッファ品を空きスロットへ戻す
                // Restore buffered items into an empty slot even before a splitter reconnects
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    if (blockInventory.GetItem(i).Id != ItemMaster.EmptyItemId) continue;
                    blockInventory.SetItem(i, item);
                    return true;
                }
                return false;
            }

            void InsertTransitToPlayer(List<(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)> transit, IOpenableInventory inventory)
            {
                var items = transit.Select(t => ServerContext.ItemStackFactory.Create(t.itemId, 1, t.itemInstanceId)).ToList();
                if (items.Count != 0) inventory.InsertItem(items);
            }

            #endregion
        }
    }
}
