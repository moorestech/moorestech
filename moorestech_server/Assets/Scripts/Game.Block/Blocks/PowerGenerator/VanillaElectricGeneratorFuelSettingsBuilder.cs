using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.PowerGenerator
{
    /// <summary>
    /// 発電機マスタから燃料設定辞書（燃料ID → 設定とtick数）を作る。
    /// Builds fuel setting dictionaries (fuel id to setting and ticks) from the generator master.
    /// </summary>
    internal static class VanillaElectricGeneratorFuelSettingsBuilder
    {
        internal static Dictionary<ItemId, (FuelItemsElement Setting, uint Ticks)> BuildItemFuelSettings(ElectricGeneratorBlockParam param)
        {
            var settings = new Dictionary<ItemId, (FuelItemsElement Setting, uint Ticks)>();
            if (param.FuelItems == null) return settings;

            foreach (var fuelItem in param.FuelItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(fuelItem.ItemGuid);
                var ticks = GameUpdater.SecondsToTicks(fuelItem.Time);
                settings[itemId] = (fuelItem, ticks);
            }

            return settings;
        }

        internal static Dictionary<FluidId, (FuelFluidsElement Setting, uint Ticks)> BuildFluidFuelSettings(ElectricGeneratorBlockParam param)
        {
            var settings = new Dictionary<FluidId, (FuelFluidsElement Setting, uint Ticks)>();
            if (param.FuelFluids == null) return settings;

            foreach (var fuelFluid in param.FuelFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(fuelFluid.FluidGuid);
                var ticks = GameUpdater.SecondsToTicks(fuelFluid.Time);
                settings[fluidId] = (fuelFluid, ticks);
            }

            return settings;
        }
    }
}
