using System;
using System.Collections.Generic;
using Core.Inventory;
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

        // 狙った座標にそもそも鉱脈が無い
        // No vein exists at the aimed position at all
        VeinNotFound,

        // 座標に鉱脈はあるが、狙ったveinGuidと違う
        // A vein exists at the position but it is not the aimed veinGuid
        VeinGuidMismatch,

        // 鉱脈は一致したがマスタが手掘りを許していない
        // The vein matches but its master forbids hand mining
        HandMiningNotAllowed,

        NoTool,
        ToolMismatch,
        CooldownNotElapsed,

        // 取得物を受け取れないため採掘を成立させない
        // Mining is refused because the drops could not be received
        InventoryFull,
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

        public VeinMiningResult TryMine(int playerId, Guid veinGuid, Vector3Int position, IItemStack equippedItem, IOpenableInventory earnedItemsDestination, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            var findResult = FindMinableVein(veinGuid, position, out var vein, out var minableParam);
            if (findResult != VeinMiningResult.Success) return findResult;

            // 装備スロットが空ならどのツールにも一致しない
            // Bare hands match no tools
            if (equippedItem.Id == ItemMaster.EmptyItemId) return VeinMiningResult.NoTool;

            if (!TryResolveUsableTool(equippedItem.Id, minableParam.HandMiningTools, out var usableTool)) return VeinMiningResult.ToolMismatch;

            if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return VeinMiningResult.CooldownNotElapsed;

            // 受け取れない取得物は消滅するので、打撃を記録する前に空きを確かめる
            // Undeliverable drops would vanish, so verify the free space before recording the swing
            var candidateItems = CreateEarnedItems(vein.VeinItemId, minableParam);
            if (!earnedItemsDestination.InsertionCheck(candidateItems)) return VeinMiningResult.InventoryFull;

            _cooldownService.RecordAttack(playerId);
            earnedItems = candidateItems;
            return VeinMiningResult.Success;

            #region Internal

            VeinMiningResult FindMinableVein(Guid aimedVeinGuid, Vector3Int pos, out IItemMapVein foundVein, out MinableHandMiningParam foundParam)
            {
                foundVein = null;
                foundParam = null;

                var anyVeinAtPosition = false;
                foreach (var overVein in ServerContext.ItemMapVeinDatastore.GetVeinsContainingCell(pos))
                {
                    anyVeinAtPosition = true;

                    // 重なった別鉱脈を掘らせない
                    // An overlapping vein must not be mined
                    if (overVein.VeinGuid != aimedVeinGuid) continue;

                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(overVein.VeinGuid);
                    if (element.HandMiningParam is not MinableHandMiningParam minable) return VeinMiningResult.HandMiningNotAllowed;

                    foundVein = overVein;
                    foundParam = minable;
                    return VeinMiningResult.Success;
                }

                return anyVeinAtPosition ? VeinMiningResult.VeinGuidMismatch : VeinMiningResult.VeinNotFound;
            }

            List<IItemStack> CreateEarnedItems(ItemId itemId, MinableHandMiningParam param)
            {
                // 個数を一様抽選
                // Sample count uniformly
                var count = _random.Next(param.MinCount, param.MaxCount + 1);
                return ServerContext.ItemStackFactory.CreateSplitStacks(itemId, count);
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
