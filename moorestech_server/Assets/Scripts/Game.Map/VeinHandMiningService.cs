using System;
using System.Collections.Generic;
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
    ///     vein手掘りのサーバ権威判定。座標→vein解決・ツール照合・1振り1ドロップを担う
    ///     Server-authoritative vein hand mining: position→vein resolution, tool matching, one drop per swing
    /// </summary>
    public class VeinHandMiningService
    {
        private readonly MiningCooldownService _cooldownService;
        private readonly Random _random = new();

        public VeinHandMiningService(MiningCooldownService cooldownService)
        {
            _cooldownService = cooldownService;
        }

        public VeinMiningResult TryMine(int playerId, Vector3Int position, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 座標上のitem veinからminable設定のものを探す
            // Find a minable-configured item vein over the position
            if (!TryFindMinableVein(position, out var vein, out var minableParam)) return VeinMiningResult.NoMinableVein;

            // 素手はどのツールにも一致しない
            // Bare hands match no tools
            if (equippedItem.Id == ItemMaster.EmptyItemId) return VeinMiningResult.NoTool;

            // 装備中ツールをhandMiningToolsと照合する
            // Match the equipped tool against handMiningTools
            if (!TryResolveUsableTool(equippedItem.Id, minableParam.HandMiningTools, out var usableTool)) return VeinMiningResult.ToolMismatch;

            // mapObject採掘と共有のクールダウンで1振り制限を守る
            // The cooldown shared with mapObject mining enforces one swing at a time
            if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return VeinMiningResult.CooldownNotElapsed;

            _cooldownService.RecordAttack(playerId);
            earnedItems = CreateEarnedItems(vein.VeinItemId, minableParam);
            return VeinMiningResult.Success;

            #region Internal

            bool TryFindMinableVein(Vector3Int pos, out IItemMapVein foundVein, out MinableHandMiningParam foundParam)
            {
                foundVein = null;
                foundParam = null;
                foreach (var overVein in ServerContext.ItemMapVeinDatastore.GetOverVeins(pos))
                {
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(overVein.VeinGuid);
                    if (element.HandMiningParam is not MinableHandMiningParam minable) continue;
                    foundVein = overVein;
                    foundParam = minable;
                    return true;
                }

                return false;
            }

            bool TryResolveUsableTool(ItemId equippedItemId, HandMiningToolsElement[] tools, out HandMiningToolsElement usableTool)
            {
                usableTool = null;
                var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
                foreach (var tool in tools)
                {
                    if (tool.ToolItemGuid != equippedItemGuid) continue;
                    usableTool = tool;
                    return true;
                }

                return false;
            }

            List<IItemStack> CreateEarnedItems(ItemId itemId, MinableHandMiningParam param)
            {
                // 1振りごとにminCount〜maxCountを一様抽選し、1スタックとして返す
                // Roll minCount..maxCount uniformly for each swing and return one stack
                var count = _random.Next(param.MinCount, param.MaxCount + 1);
                return new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, count) };
            }

            #endregion
        }
    }
}
