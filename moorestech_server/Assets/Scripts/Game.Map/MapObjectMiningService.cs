using System;
using System.Collections.Generic;
using System.Diagnostics;
using Core.Item.Interface;
using Core.Master;
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
    }

    /// <summary>
    ///     採掘のダメージ算出とクールダウン検証をサーバ側で行う
    ///     Resolves mining damage and validates the cooldown on the server side
    /// </summary>
    public class MapObjectMiningService
    {
        // クールダウン判定の許容率。クライアントはattackSpeed間隔ちょうどで送るためジッタ余裕を持たせる
        // Cooldown tolerance; clients send at exactly attackSpeed intervals, so allow jitter
        private const double CooldownMarginRate = 0.9;

        // 1プレイヤー1振りを保証する最終打撃時刻
        // Last-hit timestamps enforcing one swing at a time per player
        private readonly Dictionary<int, long> _lastAttackTimestamps = new();

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

        public MiningAttackResult TryAttack(int playerId, IMapObject mapObject, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 破壊済みへの打撃は何も起こさない
            // A hit on an already destroyed object does nothing
            if (mapObject.IsDestroyed) return MiningAttackResult.AlreadyDestroyed;

            var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);

            // PickUpはツール不要の一撃取得
            // PickUp requires no tool and destroys in one hit
            if (mapObjectElement.MiningType == MapObjectMasterElement.MiningTypeConst.PickUp)
                return ForceDestroy(mapObject, out earnedItems) ? MiningAttackResult.Success : MiningAttackResult.AlreadyDestroyed;

            // 素手ではどのminingToolsにも一致しないので早期に弾く（空IDはItemMaster参照で例外になる）
            // Bare hands match no miningTools, so reject early; the empty id would throw in ItemMaster
            if (equippedItem.Id == ItemMaster.EmptyItemId) return MiningAttackResult.NoTool;

            // 装備中ツールとminingToolsを照合しダメージを解決する
            // Resolve damage by matching the equipped tool against miningTools
            var miningTools = ((MiningMiningParam)mapObjectElement.MiningParam).MiningTools;
            if (!TryResolveUsableTool(equippedItem.Id, miningTools, out var usableTool))
                return MiningAttackResult.ToolMismatch;

            return TryAttackWithTool(usableTool, out earnedItems);

            #region Internal

            MiningAttackResult TryAttackWithTool(MiningToolsElement miningTool, out List<IItemStack> toolEarnedItems)
            {
                toolEarnedItems = null;

                // 前回打撃からattackSpeed×許容率秒未満の連打は捨てる
                // Drop repeat hits that arrive within attackSpeed * tolerance seconds of the previous one
                var now = Stopwatch.GetTimestamp();
                if (_lastAttackTimestamps.TryGetValue(playerId, out var lastAttackTimestamp))
                {
                    var elapsedSeconds = (now - lastAttackTimestamp) / (double)Stopwatch.Frequency;
                    if (elapsedSeconds < miningTool.AttackSpeed * CooldownMarginRate)
                        return MiningAttackResult.CooldownNotElapsed;
                }
                _lastAttackTimestamps[playerId] = now;

                toolEarnedItems = mapObject.Attack(miningTool.Damage);
                return MiningAttackResult.Success;
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
