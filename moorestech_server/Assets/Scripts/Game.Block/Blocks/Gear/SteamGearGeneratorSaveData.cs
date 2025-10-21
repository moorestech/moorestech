using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // セーブデータ構造: サービスから現在値を抽出してJSON化する
    // Save-data structure: extracts current values from services for JSON serialization
    public class SteamGearGeneratorSaveData
    {
        public string CurrentState { get; set; }
        public float StateElapsedTime { get; set; }
        public float SteamConsumptionRate { get; set; }
        public float RateAtDecelerationStart { get; set; }
        public List<ItemStackSaveJsonObject> Items { get; set; }
        public string ActiveFuelType { get; set; }
        public double RemainingFuelTime { get; set; }
        public string CurrentFuelItemGuidStr { get; set; }
        public string CurrentFuelFluidGuidStr { get; set; }

        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        // サービスから現在の情報を取得してプロパティへ展開する
        // Populate properties by querying live services for the current generator state
        public SteamGearGeneratorSaveData(
            SteamGearGeneratorStateService stateService,
            SteamGearGeneratorFuelService fuelService,
            OpenableInventoryItemDataStoreService inventoryService)
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

            Items = new List<ItemStackSaveJsonObject>();
            var slotSize = inventoryService.GetSlotSize();
            for (var i = 0; i < slotSize; i++)
            {
                Items.Add(new ItemStackSaveJsonObject(inventoryService.GetItem(i)));
            }
        }

        // デシリアライズ用の既定コンストラクタ
        // Default constructor reserved for JSON deserialization
        public SteamGearGeneratorSaveData()
        {
            Items = new List<ItemStackSaveJsonObject>();
        }
    }
}
