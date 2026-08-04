using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class MapVeinMasterUtil
    {
        public static bool Validate(MapVeinMasterElement[] mapVeins, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinParamGuidValidation();
            errorLogs += HandMiningValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

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
                // fluid鉱脈のminable禁止とminable設定の内部整合を検証する
                // Validate fluid veins are not minable and minable settings are internally consistent
                var logs = "";
                foreach (var element in mapVeins)
                {
                    if (element.HandMiningParam is not MinableHandMiningParam minableHandMiningParam) continue;

                    // fluid鉱脈を手掘り可能にする設定を拒否する
                    // Reject settings that make fluid veins hand-minable
                    if (element.VeinParam is FluidVeinParam)
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} fluid veinはminableにできません\n";
                    }

                    // minable鉱脈に少なくとも1つのツールを要求する
                    // Require at least one tool for each minable vein
                    if (minableHandMiningParam.HandMiningTools.Length == 0)
                    {
                        logs += $"[MapVeinMaster] Name:{element.VeinName} handMiningToolsが空です\n";
                    }

                    // 配列の全toolItemGuidが実在アイテムを参照することを検証する
                    // Validate every toolItemGuid in the array references an existing item
                    foreach (var handMiningTool in minableHandMiningParam.HandMiningTools)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(handMiningTool.ToolItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has invalid ToolItemGuid:{handMiningTool.ToolItemGuid}\n";
                        }
                    }

                    // 採掘ドロップ数の最小値と範囲順序を検証する
                    // Validate the minimum drop count and range order
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
