using System;
using Core.Master;
using Core.Update;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // 発電機のステート内容をシリアライズするための単純なDTO
    // Simple DTO that serialises the generator state for persistence
    [Serializable]
    public class FuelGearGeneratorSaveData
    {
        public string CurrentState;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        public double StateElapsedSeconds;
        public float SteamConsumptionRate;
        public float RateAtDecelerationStart;

        public string ActiveFuelType;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        public double RemainingFuelSeconds;
        public string CurrentFuelItemGuidStr;
        public string CurrentFuelFluidGuidStr;

        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        public FuelGearGeneratorSaveData(
            FuelGearGeneratorStateService stateService,
            FuelGearGeneratorFuelService fuelService)
        {
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            CurrentState = stateService.CurrentState.ToString();
            StateElapsedSeconds = GameUpdater.TicksToSeconds(stateService.StateElapsedTicks);
            SteamConsumptionRate = stateService.SteamConsumptionRate;
            RateAtDecelerationStart = stateService.RateAtDecelerationStart;

            ActiveFuelType = fuelService.CurrentFuelType.ToString();
            RemainingFuelSeconds = GameUpdater.TicksToSeconds(fuelService.RemainingFuelTicks);

            if (fuelService.CurrentFuelItemId != ItemMaster.EmptyItemId)
            {
                var itemGuid = MasterHolder.ItemMaster.GetItemMaster(fuelService.CurrentFuelItemId).ItemGuid;
                CurrentFuelItemGuidStr = itemGuid.ToString();
            }

            if (fuelService.CurrentFuelFluidId != FluidMaster.EmptyFluidId)
            {
                var fluidGuid = MasterHolder.FluidMaster.GetFluidMaster(fuelService.CurrentFuelFluidId).FluidGuid;
                CurrentFuelFluidGuidStr = fluidGuid.ToString();
            }
        }

        public FuelGearGeneratorSaveData()
        {
        }
    }
}
