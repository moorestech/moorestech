using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Map.Interface.MapObject;
using Mooresmaster.Model.MapModule;

namespace Game.Map
{
    /// <summary>
    ///     採掘のダメージ算出とクールダウン検証をサーバ側で行う
    ///     Resolves mining damage and validates the cooldown on the server side
    /// </summary>
    public class MapObjectMiningService
    {
        // クールダウン判定の許容率。クライアントはattackSpeed間隔ちょうどで送るためジッタ余裕を持たせる
        // Cooldown tolerance; clients send at exactly attackSpeed intervals, so allow jitter
        private const double CooldownMarginRate = 0.9;

        // プレイヤー×mapObjectごとの最終打撃時刻。クールダウン検証に使う
        // Last hit time per (player, mapObject) for cooldown validation
        private readonly Dictionary<(int playerId, int instanceId), DateTime> _lastAttackTimes = new();

        /// <summary>
        ///     ツール照合もクールダウンも介さず一撃で破壊する
        ///     Destroys in a single hit without tool matching or cooldown
        /// </summary>
        public bool ForceDestroy(IMapObject mapObject, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 破壊済みへの打撃は何も起こさない
            // A hit on an already destroyed object does nothing
            if (mapObject.IsDestroyed) return false;

            earnedItems = mapObject.Attack(int.MaxValue);
            return true;
        }

        public bool TryAttack(int playerId, IMapObject mapObject, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 破壊済みへの打撃は何も起こさない
            // A hit on an already destroyed object does nothing
            if (mapObject.IsDestroyed) return false;

            var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);

            // PickUpはツール不要の一撃取得
            // PickUp requires no tool and destroys in one hit
            if (mapObjectElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp) return ForceDestroy(mapObject, out earnedItems);

            // 素手ではどのminingToolsにも一致しないので早期に弾く（空IDはItemMaster参照で例外になる）
            // Bare hands match no miningTools, so reject early; the empty id would throw in ItemMaster
            if (equippedItem.Id == ItemMaster.EmptyItemId) return false;

            // 装備中ツールとminingToolsを照合しダメージを解決する
            // Resolve damage by matching the equipped tool against miningTools
            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItem.Id).ItemGuid;
            var miningTools = ((MiningMiningParam)mapObjectElement.MiningParam).MiningTools;
            foreach (var miningTool in miningTools)
            {
                if (miningTool.ToolItemGuid != equippedItemGuid) continue;
                return TryAttackWithTool(miningTool, out earnedItems);
            }

            return false;

            #region Internal

            bool TryAttackWithTool(MiningToolsElement miningTool, out List<IItemStack> toolEarnedItems)
            {
                toolEarnedItems = null;

                // 前回打撃からattackSpeed×許容率秒未満の連打は捨てる
                // Drop repeat hits that arrive within attackSpeed * tolerance seconds of the previous one
                var key = (playerId, mapObject.InstanceId);
                var now = DateTime.UtcNow;
                if (_lastAttackTimes.TryGetValue(key, out var lastAttackTime) && (now - lastAttackTime).TotalSeconds < miningTool.AttackSpeed * CooldownMarginRate) return false;
                _lastAttackTimes[key] = now;

                toolEarnedItems = mapObject.Attack(miningTool.Damage);
                return true;
            }

            #endregion
        }
    }
}
