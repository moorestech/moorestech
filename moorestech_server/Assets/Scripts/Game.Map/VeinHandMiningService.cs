using System;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Map.Interface.Vein;
using Mooresmaster.Model.MapModule;
using UnityEngine;
using Random = System.Random;

namespace Game.Map
{
    public enum VeinMiningResult
    {
        Success,
        NoMinableVein,
        NoTool,
        ToolMismatch,
        CooldownNotElapsed,
    }

    /// <summary>
    ///     vein手掘りの権威判定
    ///     Authority check for vein hand mining
    /// </summary>
    public class VeinHandMiningService
    {
        private readonly MiningCooldownService _cooldownService;
        private readonly Random _random = new();

        public VeinHandMiningService(MiningCooldownService cooldownService)
        {
            _cooldownService = cooldownService;
        }

        public VeinMiningResult TryMine(int playerId, Guid veinGuid, Vector3Int position, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            if (!TryFindMinableVein(veinGuid, position, out var vein, out var minableParam)) return VeinMiningResult.NoMinableVein;

            // 素手はどのツールにも一致しない
            // Bare hands match no tools
            if (equippedItem.Id == ItemMaster.EmptyItemId) return VeinMiningResult.NoTool;

            if (!TryResolveUsableTool(equippedItem.Id, minableParam.HandMiningTools, out var usableTool)) return VeinMiningResult.ToolMismatch;

            if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return VeinMiningResult.CooldownNotElapsed;

            _cooldownService.RecordAttack(playerId);
            earnedItems = CreateEarnedItems(vein.VeinItemId, minableParam);
            return VeinMiningResult.Success;

            #region Internal

            bool TryFindMinableVein(Guid aimedVeinGuid, Vector3Int pos, out IItemMapVein foundVein, out MinableHandMiningParam foundParam)
            {
                foundVein = null;
                foundParam = null;
                foreach (var overVein in ServerContext.ItemMapVeinDatastore.GetOverVeins(pos))
                {
                    // 重なった別鉱脈を掘らせない
                    // An overlapping vein must not be mined
                    if (overVein.VeinGuid != aimedVeinGuid) continue;

                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(overVein.VeinGuid);
                    if (element.HandMiningParam is not MinableHandMiningParam minable) continue;
                    foundVein = overVein;
                    foundParam = minable;
                    return true;
                }

                return false;
            }

            List<IItemStack> CreateEarnedItems(ItemId itemId, MinableHandMiningParam param)
            {
                // 個数を一様抽選
                // Sample count uniformly
                var count = _random.Next(param.MinCount, param.MaxCount + 1);
                var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);

                // 最大スタック数を超える場合は分割して追加
                // Split into multiple stacks if exceeding max stack size
                var items = new List<IItemStack>();
                var fullItemCount = count / maxStack;
                for (var i = 0; i < fullItemCount; i++)
                {
                    items.Add(ServerContext.ItemStackFactory.Create(itemId, maxStack));
                }

                // あまりを追加する
                // Add remainder
                var remainCount = count % maxStack;
                if (remainCount != 0)
                {
                    items.Add(ServerContext.ItemStackFactory.Create(itemId, remainCount));
                }

                return items;
            }

            #endregion
        }

        /// <summary>
        ///     装備ツールが手掘り許可に含まれるか照合する
        ///     Match the equipped tool against the allowed hand-mining tools
        /// </summary>
        public static bool TryResolveUsableTool(ItemId equippedItemId, HandMiningToolsElement[] tools, out HandMiningToolsElement usableTool)
        {
            usableTool = null;
            if (equippedItemId == ItemMaster.EmptyItemId) return false;

            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var tool in tools)
            {
                if (tool.ToolItemGuid != equippedItemGuid) continue;
                usableTool = tool;
                return true;
            }

            return false;
        }
    }
}
