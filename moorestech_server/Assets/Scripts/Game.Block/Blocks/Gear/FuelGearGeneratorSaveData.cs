using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // 発電機のステート内容をシリアライズするための単純なDTO
    // Simple DTO that serialises the generator state for persistence
    [Serializable]
    public class FuelGearGeneratorSaveData
    {
        public string CurrentState;
        public uint StateElapsedTicks;
        public float SteamConsumptionRate;
        public float RateAtDecelerationStart;

        public string ActiveFuelType;
        public uint RemainingFuelTicks;
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
            CurrentState = stateService.CurrentState.ToString();
            StateElapsedTicks = stateService.StateElapsedTicks;
            SteamConsumptionRate = stateService.SteamConsumptionRate;
            RateAtDecelerationStart = stateService.RateAtDecelerationStart;

            ActiveFuelType = fuelService.CurrentFuelType.ToString();
            RemainingFuelTicks = fuelService.RemainingFuelTicks;

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
