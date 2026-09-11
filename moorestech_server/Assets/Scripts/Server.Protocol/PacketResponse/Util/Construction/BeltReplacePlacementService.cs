using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 1セルの張替え。撤去前に設置の成否を確定させ、通ったセルだけを「返却→撤去→設置→消費→搬送品復元」で処理する
    /// One-cell replace; the outcome is settled before anything is removed, and only a validated cell runs refund, removal, placement, consumption and transit restore
    /// </summary>
    public class BeltReplacePlacementService
    {
        private static readonly IReadOnlyList<IItemStack> NoRefund = Array.Empty<IItemStack>();

        private readonly ConstructionWalletService _constructionWallet;
        private readonly PlacementTargetCatalog _placementTargetCatalog;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public BeltReplacePlacementService(ConstructionWalletService constructionWallet, PlacementTargetCatalog placementTargetCatalog, IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _constructionWallet = constructionWallet;
            _placementTargetCatalog = placementTargetCatalog;
            _gameUnlockStateDataController = gameUnlockStateDataController;
        }

        public BeltReplaceResult Replace(PlaceInfoMessagePack placeInfo, IOpenableInventory inventory, int playerId, bool isFreePlacement)
        {
            var position = placeInfo.Position;
            var newBlockId = placeInfo.BlockId;

            // 既設と手持ちが同じロールのベルトファミリー員であること。BeltConveyorFamilyValidatorがメンバーを1x1x1に限っているので占有は既設のまま使える
            // Both blocks must be belt family members of the same role; BeltConveyorFamilyValidator restricts members to 1x1x1, so the existing footprint carries over as-is
            var oldBlock = ServerContext.WorldBlockDatastore.GetBlock(position);
            if (oldBlock == null) return Reject(BeltReplaceResult.Rejected, "no block at the cell");
            if (!TryGetRole(oldBlock.BlockId, out var oldRole)) return Reject(BeltReplaceResult.Rejected, $"existing block {oldBlock.BlockId} is not a belt family member");
            if (!TryGetRole(newBlockId, out var newRole)) return Reject(BeltReplaceResult.Rejected, $"held block {newBlockId} is not a belt family member");
            if (oldRole != newRole) return Reject(BeltReplaceResult.Rejected, $"role mismatch existing:{oldRole} held:{newRole}");
            if (oldBlock.BlockId == newBlockId) return BeltReplaceResult.NoChange;

            var newBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(newBlockId);
            if (!_placementTargetCatalog.IsBlockUnlocked(newBlockMaster.BlockGuid, _gameUnlockStateDataController, false)) return Reject(BeltReplaceResult.NotUnlocked, $"held block {newBlockId} is locked");

            // 退避する搬送品と財布の指示を集める。財布への問い合わせはCommitを呼ぶまで何も変えない
            // Gather the transit items and the wallet's instructions; asking the wallet changes nothing until Commit is called
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            var transitItems = BeltConveyorTransitCarryOver.Collect(oldBelt);
            var removalPlan = _constructionWallet.PlanRemoval(MasterHolder.BlockMaster.GetBlockMaster(oldBlock.BlockId), oldBlock.BlockInstanceId, playerId);
            var placementPlan = _constructionWallet.PlanPlacement(newBlockMaster, playerId);

            // 無料設置が免除するのはコスト検証・消費・返却だけで、搬送品の受け皿検証は必ず走る
            // Free placement waives only the cost checks, consumption and refund; the transit receptacle check always runs
            var refundItems = isFreePlacement ? NoRefund : removalPlan.ItemsToRefund;
            if (!HasRoomForReturnedItems()) return Reject(BeltReplaceResult.InventoryFull, "no room for the refund and transit items");
            if (!isFreePlacement && !CanPayNewCost()) return Reject(BeltReplaceResult.CostShortage, $"new construction cost of {newBlockId} is short");

            // ここから先は検証済みなので、撤去と設置を1セル分まとめて実行する
            // Everything below is validated, so the removal and the placement run as one unit for this cell
            var direction = oldBlock.BlockPositionInfo.BlockDirection;
            var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            ServerContext.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.Replace);

            // 無料設置は撤去側の財布も飛ばすため、旧ブロックの課金元エントリと残り設置数が戻らない（デバッグトグル限定の非対称）
            // Free placement also skips the removal wallet, so the old block's payer entry and remaining count are never returned (an asymmetry limited to the debug toggle)
            if (!isFreePlacement)
            {
                _constructionWallet.CommitRemoval(removalPlan);
                ReturnToPlayer(refundItems);
            }

            if (!ServerContext.WorldBlockDatastore.TryAddBlock(newBlockId, position, direction, createParams, out var newBlock))
            {
                // 検証済みのセルで設置が失敗するのは設計上あり得ない。巻き戻さず、搬送品だけプレイヤーへ逃がす
                // Placement cannot fail on a validated cell by design; nothing is rolled back and only the transit items are handed back
                Debug.LogError($"[BeltReplace] placement failed after removal at {position} held:{newBlockId}");
                ReturnToPlayer(ToItemStacks(transitItems));
                return BeltReplaceResult.Rejected;
            }

            if (!isFreePlacement) _constructionWallet.CommitPlacement(placementPlan, inventory, newBlock.BlockInstanceId);

            // 搬送品を新ベルトへ戻す。進行率の扱いと収まらない分の判断はCarryOver側が持つ
            // Hand the transit items back to the new belt; CarryOver owns how the progress maps and what does not fit
            var overflow = BeltConveyorTransitCarryOver.Restore(newBlock.GetComponent<VanillaBeltConveyorComponent>(), transitItems);
            ReturnToPlayer(ToItemStacks(overflow));
            return BeltReplaceResult.Replaced;

            #region Internal

            bool TryGetRole(BlockId blockId, out BeltConveyorRole role)
            {
                role = default;
                return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) && family.TryGetRole(blockId, out role);
            }

            bool HasRoomForReturnedItems()
            {
                // 最悪ケースは「返却品と搬送品が全部プレイヤー行き」。消費は空きを増やす側なので、この検証が最も厳しい
                // The worst case is every refund and transit item going to the player; consumption only frees room, so this is the tightest check
                var worstCase = new List<IItemStack>(refundItems);
                worstCase.AddRange(ToItemStacks(transitItems));
                return worstCase.Count == 0 || inventory.InsertionCheck(worstCase);
            }

            bool CanPayNewCost()
            {
                // 新コストは「今の所持品＋旧ブロックの返却品」で賄えればよい。返却は消費より先に行われる
                // The new cost only has to be covered by the current holdings plus the old block's refund, which lands before the consumption
                var available = new List<IItemStack>(inventory.InventoryItems);
                available.AddRange(refundItems);
                return ConstructionCostService.HasRequiredItems(placementPlan.ItemsToConsume, available);
            }

            void ReturnToPlayer(IReadOnlyList<IItemStack> items)
            {
                if (items.Count == 0) return;
                var remainder = inventory.InsertItem(new List<IItemStack>(items));

                // 事前検証で受け皿を確保しているため残りは出ない。出たなら検証と実行がずれている
                // The pre-validation reserved the room, so no remainder can appear; one means validation and execution have diverged
                if (remainder.Count != 0) Debug.LogError($"[BeltReplace] could not return {DescribeItems(remainder)} to player {playerId} at {position}");

                // 事故時に何が消えたかを追えるようログ用の内訳を作る
                // Builds the breakdown the log needs so a lost item can be traced afterwards
                string DescribeItems(IReadOnlyList<IItemStack> stacks)
                {
                    var descriptions = new List<string>();
                    foreach (var stack in stacks)
                    {
                        descriptions.Add($"{stack.Id}x{stack.Count}");
                    }
                    return string.Join(",", descriptions);
                }
            }

            BeltReplaceResult Reject(BeltReplaceResult result, string reason)
            {
                Debug.Log($"[BeltReplace] rejected at {position} held:{newBlockId} reason:{reason}");
                return result;
            }

            List<IItemStack> ToItemStacks(IReadOnlyList<BeltTransitItem> beltItems)
            {
                var result = new List<IItemStack>();
                foreach (var beltItem in beltItems)
                {
                    result.Add(ServerContext.ItemStackFactory.Create(beltItem.ItemId, 1, beltItem.ItemInstanceId));
                }
                return result;
            }

            #endregion
        }

        /// <summary>
        /// 1セルの張替え結果。呼び出し側はこれを集計してプレイヤーへ通知する
        /// The outcome of one replace cell; the caller aggregates these and notifies the player
        /// </summary>
        public enum BeltReplaceResult
        {
            Replaced,
            NoChange,
            NotUnlocked,
            CostShortage,
            InventoryFull,
            Rejected,
        }
    }
}
