using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // 発電機のステート内容をシリアライズするための単純なDTO
    // Simple DTO that serialises the generator state for persistence
    [Serializable]
    public class SteamGearGeneratorSaveData
    {
        public string CurrentState;
        public float StateElapsedTime;
        public float SteamConsumptionRate;
        public float RateAtDecelerationStart;
        
        public string ActiveFuelType;
        public double RemainingFuelTime;
        public string CurrentFuelItemGuidStr;
        public string CurrentFuelFluidGuidStr;

        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        public SteamGearGeneratorSaveData(
            SteamGearGeneratorStateService stateService,
            SteamGearGeneratorFuelService fuelService)
        {
            CurrentState = stateService.CurrentState.ToString();
            StateElapsedTime = stateService.StateElapsedTime;
            SteamConsumptionRate = stateService.SteamConsumptionRate;
            RateAtDecelerationStart = stateService.RateAtDecelerationStart;

            ActiveFuelType = fuelService.CurrentFuelType.ToString();
            RemainingFuelTime = fuelService.RemainingFuelTime;
            CurrentFuelItemGuidStr = MasterHolder.ItemMaster.GetItemMaster(fuelService.CurrentFuelItemId).ItemGuid.ToString();
            CurrentFuelFluidGuidStr = MasterHolder.FluidMaster.GetFluidMaster(fuelService.CurrentFuelFluidId).FluidGuid.ToString();
        }

        public SteamGearGeneratorSaveData()
        {
        }
    }
}
