using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class MapVeinMasterUtil
    {
        public static bool Validate(MapVeinMasterElement[] mapVeins, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinParamGuidValidation();
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

            #endregion
        }
    }
}
