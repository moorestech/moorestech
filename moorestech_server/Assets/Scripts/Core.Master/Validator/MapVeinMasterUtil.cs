using System;
using System.Collections.Generic;
using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class MapVeinMasterUtil
    {
        public static bool Validate(MapVeinMasterElement[] mapVeins, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinParamGuidValidation();
            errorLogs += OutcropAddressablePathValidation();
            errorLogs += HandMiningValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string OutcropAddressablePathValidation()
            {
                // 全鉱脈が露頭を立てるので空を弾く
                // Every vein raises an outcrop, so an empty path is rejected
                var logs = "";
                foreach (var element in mapVeins)
                {
                    if (string.IsNullOrEmpty(element.OutcropAddressablePath))
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} outcropAddressablePathが空です\n";
                    }
                }

                return logs;
            }

            string VeinParamGuidValidation()
            {
                // veinTypeごとにitemGuid/fluidGuidが実在するかを検証する
                // Validate that itemGuid/fluidGuid exist for each veinType
                var logs = "";
                foreach (var element in mapVeins)
                {
                    if (element.VeinParam is ItemVeinParam itemVeinParam)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemVeinParam.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has invalid ItemGuid:{itemVeinParam.ItemGuid}\n";
                        }
                    }
                    else if (element.VeinParam is FluidVeinParam fluidVeinParam)
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(fluidVeinParam.FluidGuid);
                        if (fluidId == null)
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has invalid FluidGuid:{fluidVeinParam.FluidGuid}\n";
                        }
                    }
                }

                return logs;
            }

            string HandMiningValidation()
            {
                // 手掘り設定を検証
                // Validate hand-mining settings
                var logs = "";
                foreach (var element in mapVeins)
                {
                    if (element.HandMiningParam is not MinableHandMiningParam minableHandMiningParam) continue;

                    // fluid手掘りを拒否
                    // Reject fluid hand-mining
                    if (element.VeinParam is FluidVeinParam)
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} fluid veinはminableにできません\n";
                    }

                    // ツール1件以上を要求
                    // Require at least one tool
                    if (minableHandMiningParam.HandMiningTools.Length == 0)
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} handMiningToolsが空です\n";
                    }

                    // 全ツールGUIDと採掘速度を検証
                    // Validate all tool GUIDs and mining speeds
                    var toolItemGuids = new HashSet<Guid>();
                    foreach (var handMiningTool in minableHandMiningParam.HandMiningTools)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(handMiningTool.ToolItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has invalid ToolItemGuid:{handMiningTool.ToolItemGuid}\n";
                        }
                        if (handMiningTool.AttackSpeed <= 0)
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has non-positive attackSpeed:{handMiningTool.AttackSpeed}\n";
                        }
                        if (!toolItemGuids.Add(handMiningTool.ToolItemGuid))
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has duplicate ToolItemGuid:{handMiningTool.ToolItemGuid}\n";
                        }
                    }

                    // ドロップ範囲を検証
                    // Validate drop range
                    if (minableHandMiningParam.MinCount < 1 || minableHandMiningParam.MaxCount < minableHandMiningParam.MinCount)
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} minCount/maxCountが不正です min:{minableHandMiningParam.MinCount} max:{minableHandMiningParam.MaxCount}\n";
                    }
                }

                return logs;
            }

            #endregion
        }
    }
}
