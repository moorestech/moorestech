using System;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // 発電機のステート内容をシリアライズするための単純なDTO
    // Simple DTO that serialises the generator state for persistence
    public class SteamGearGeneratorSaveData
    {
        public string CurrentState { get; set; }
        public float StateElapsedTime { get; set; }
        public float SteamConsumptionRate { get; set; }
        public float RateAtDecelerationStart { get; set; }
        public string ActiveFuelType { get; set; }
        public double RemainingFuelTime { get; set; }
        public string CurrentFuelItemGuidStr { get; set; }
        public string CurrentFuelFluidGuidStr { get; set; }

        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        public SteamGearGeneratorSaveData(
            SteamGearGeneratorStateService stateService,
            SteamGearGeneratorFuelService fuelService)
        {
            var stateSnapshot = stateService.CreateSnapshot();
            CurrentState = stateSnapshot.State;
            StateElapsedTime = stateSnapshot.StateElapsedTime;
            SteamConsumptionRate = stateSnapshot.SteamConsumptionRate;
            RateAtDecelerationStart = stateSnapshot.RateAtDecelerationStart;

            var fuelSnapshot = fuelService.CreateSnapshot();
            ActiveFuelType = fuelSnapshot.ActiveFuelType.ToString();
            RemainingFuelTime = fuelSnapshot.RemainingFuelTime;
            CurrentFuelItemGuidStr = fuelSnapshot.CurrentFuelItemGuid?.ToString();
            CurrentFuelFluidGuidStr = fuelSnapshot.CurrentFuelFluidGuid?.ToString();
        }

        public SteamGearGeneratorSaveData()
        {
        }
    }
}
