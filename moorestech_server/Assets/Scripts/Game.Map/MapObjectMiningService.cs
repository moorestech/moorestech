using System.Collections.Generic;
using Common.Debug;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.MapModule;

namespace Game.Map
{
    public enum MiningAttackResult
    {
        Success,
        AlreadyDestroyed,
        NoTool,
        ToolMismatch,
        CooldownNotElapsed,

        // 取得物を受け取れないため採掘を成立させない
        // Mining is refused because the drops could not be received
        InventoryFull,
    }

    /// <summary>
    ///     採掘のダメージ算出とクールダウン検証をサーバ側で行う
    ///     Resolves mining damage and validates the cooldown on the server side
    /// </summary>
    public class MapObjectMiningService
    {
        private readonly MiningCooldownService _cooldownService;

        public MapObjectMiningService(MiningCooldownService cooldownService)
        {
            _cooldownService = cooldownService;
        }

        public MiningAttackResult TryAttack(int playerId, IMapObject mapObject, IItemStack equippedItem, IOpenableInventory earnedItemsDestination, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 破壊済みへの打撃は何も起こさない。デバッグフラグ読みのファイルIOもここで打ち切る
            // A hit on an already destroyed object does nothing; this also cuts off the debug flag file IO
            if (mapObject.IsDestroyed) return MiningAttackResult.AlreadyDestroyed;

            // 受け取れない取得物は消滅するので、対象を削る前に空きを確かめる
            // Undeliverable drops would vanish, so verify the free space before wearing the target down
            var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);
            if (!CanReceiveEarnItems(mapObjectElement)) return MiningAttackResult.InventoryFull;

            // PickUpと高速採掘デバッグはツール照合もクールダウンも介さず一撃で破壊する
            // PickUp and the debug super-mine destroy in one hit without tool matching or cooldown
            if (mapObjectElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp ||
                DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.MapObjectSuperMine))
            {
                earnedItems = mapObject.Attack(int.MaxValue);
                return MiningAttackResult.Success;
            }

            // 装備スロットが空ならどのminingToolsにも一致しないので早期に弾く（空IDはItemMaster参照で例外になる）
            // Bare hands match no miningTools, so reject early; the empty id would throw in ItemMaster
            if (equippedItem.Id == ItemMaster.EmptyItemId) return MiningAttackResult.NoTool;

            // 装備中ツールとminingToolsを照合しダメージを解決する
            // Resolve damage by matching the equipped tool against miningTools
            var miningTools = ((MiningMiningParam)mapObjectElement.MiningParam).MiningTools;
            if (!TryResolveUsableTool(equippedItem.Id, miningTools, out var usableTool))
                return MiningAttackResult.ToolMismatch;

            // 前回打撃からattackSpeed×許容率tick未満の連打は捨てる
            // Drop repeat hits that arrive within attackSpeed * tolerance ticks of the previous one
            if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return MiningAttackResult.CooldownNotElapsed;

            _cooldownService.RecordAttack(playerId);
            earnedItems = mapObject.Attack(usableTool.Damage);
            return MiningAttackResult.Success;

            #region Internal

            bool CanReceiveEarnItems(MapObjectMasterElement element)
            {
                // 実際の取得は閾値通過時のみだが、通過し得る最大量で見ておけば消滅は起きない
                // The real drop happens only on a threshold crossing, but checking the largest possible amount rules out any loss
                var maximumEarnItems = new List<IItemStack>();
                foreach (var earnItem in element.EarnItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(earnItem.ItemGuid);
                    maximumEarnItems.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(itemId, earnItem.MaxCount));
                }

                return earnedItemsDestination.InsertionCheck(maximumEarnItems);
            }

            #endregion
        }

        public static bool TryResolveUsableTool(ItemId equippedItemId, MiningToolsElement[] miningTools, out MiningToolsElement usableTool)
        {
            usableTool = null;
            if (equippedItemId == ItemMaster.EmptyItemId) return false;

            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var miningTool in miningTools)
            {
                if (miningTool.ToolItemGuid != equippedItemGuid) continue;
                usableTool = miningTool;
                return true;
            }

            return false;
        }
    }
}
