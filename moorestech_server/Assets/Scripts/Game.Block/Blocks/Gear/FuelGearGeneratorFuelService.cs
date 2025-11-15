using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// FuelGearGenerator専用の燃料管理を集約するサービスクラス
    /// Service dedicated to managing fuel consumption for the FuelGearGenerator
    /// </summary>
    public class FuelGearGeneratorFuelService
    {
        public enum FuelType
        {
            None,
            Item,
            Fluid
        }
        
        public FuelType CurrentFuelType { get; private set; }
        public ItemId CurrentFuelItemId { get; private set; }
        public FluidId CurrentFuelFluidId { get; private set; }
        
        public double RemainingFuelTime { get; private set; }
        
        public double CurrentFuelTime
        {
            get
            {
                return CurrentFuelType switch
                {
                    FuelType.None => 0,
                    FuelType.Fluid => _fluidFuelSettings.TryGetValue(CurrentFuelFluidId, out var itemSetting) ? itemSetting.ConsumptionTime : 0,
                    FuelType.Item => _itemFuelSettings.TryGetValue(CurrentFuelItemId, out var fluidSetting) ? fluidSetting.ConsumptionTime : 0,
                    _ => 0,
                };
            }
        }
            

        // インベントリ・流体タンクと燃料設定を参照するためのフィールド群
        // Fields referencing inventories, fluid tanks, and fuel configuration tables
        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly FuelGearGeneratorFluidComponent _fluidComponent;
        private readonly Dictionary<ItemId, ItemFuelSetting> _itemFuelSettings;
        private readonly Dictionary<FluidId, FluidFuelSetting> _fluidFuelSettings;

        public FuelGearGeneratorFuelService(
            FuelGearGeneratorBlockParam param,
            OpenableInventoryItemDataStoreService inventoryService,
            FuelGearGeneratorFluidComponent fluidComponent)
        {
            _inventoryService = inventoryService;
            _fluidComponent = fluidComponent;
            _itemFuelSettings = BuildItemFuelSettings(param);
            _fluidFuelSettings = BuildFluidFuelSettings(param);

            CurrentFuelType = FuelType.None;
            CurrentFuelItemId = ItemMaster.EmptyItemId;
            CurrentFuelFluidId = FluidMaster.EmptyFluidId;
            RemainingFuelTime = 0;

            #region Internal

            static Dictionary<ItemId, ItemFuelSetting> BuildItemFuelSettings(FuelGearGeneratorBlockParam blockParam)
            {
                var settings = new Dictionary<ItemId, ItemFuelSetting>();
                if (blockParam.GearFuelItems == null) return settings;

                foreach (var element in blockParam.GearFuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(element.ItemGuid);
                    settings[itemId] = new ItemFuelSetting(Math.Max(1, element.Amount), element.ConsumptionTime);
                }

                return settings;
            }

            static Dictionary<FluidId, FluidFuelSetting> BuildFluidFuelSettings(FuelGearGeneratorBlockParam blockParam)
            {
                var settings = new Dictionary<FluidId, FluidFuelSetting>();
                if (blockParam.RequiredFluids == null) return settings;

                foreach (var element in blockParam.RequiredFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(element.FluidGuid);
                    settings[fluidId] = new FluidFuelSetting(Math.Max(0d, element.Amount), element.ConsumptionTime);
                }

                return settings;
            }

            #endregion
        }
        
        private bool IsFuelActive => CurrentFuelType != FuelType.None && RemainingFuelTime > 0;
        
        public bool HasAvailableFuel(bool allowFluidFuel)
        {
            if (IsFuelActive)
            {
                if (CurrentFuelType == FuelType.Fluid && !allowFluidFuel) return false;
                return true;
            }

            if (InventoryHasFuel()) return true;
            return allowFluidFuel && FluidHasFuel();
            
            #region Internal
            
            bool InventoryHasFuel()
            {
                if (_itemFuelSettings.Count == 0) return false;
                
                var slotSize = _inventoryService.GetSlotSize();
                for (var i = 0; i < slotSize; i++)
                {
                    var slotItem = _inventoryService.GetItem(i);
                    if (_itemFuelSettings.TryGetValue(slotItem.Id, out var setting) && slotItem.Count >= setting.Count)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            
            bool FluidHasFuel()
            {
                if (_fluidFuelSettings.Count == 0) return false;
                
                var tank = _fluidComponent.SteamTank;
                if (!_fluidFuelSettings.TryGetValue(tank.FluidId, out var setting)) return false;
                return tank.Amount >= setting.Amount;
            }
            
            #endregion
        }

        // 現在必要な燃料を確保できるかを確認し、無ければ新たに着火を試行する
        // Verify that usable fuel is available, attempting to start new combustion when needed
        public bool TryEnsureFuel(bool allowFluidFuel)
        {
            if (IsFuelActive)
            {
                if (CurrentFuelType == FuelType.Fluid && !allowFluidFuel)
                {
                    ClearFuelState();
                }
                else
                {
                    return true;
                }
            }

            if (TryStartItemFuel()) return true;
            if (!allowFluidFuel) return false;
            return TryStartFluidFuel();
            
            #region Internal
            
            
            
            // スロットから利用可能なアイテム燃料を消費し燃焼を開始する
            // Consume an available item fuel stack from inventory to ignite combustion
            bool TryStartItemFuel()
            {
                if (_itemFuelSettings.Count == 0) return false;
                
                var slotSize = _inventoryService.GetSlotSize();
                for (var i = 0; i < slotSize; i++)
                {
                    var slotItem = _inventoryService.GetItem(i);
                    if (!_itemFuelSettings.TryGetValue(slotItem.Id, out var setting)) continue;
                    if (slotItem.Count < setting.Count) continue;
                    
                    var remainder = slotItem.SubItem(setting.Count);
                    _inventoryService.SetItem(i, remainder);
                    
                    CurrentFuelType = FuelType.Item;
                    CurrentFuelItemId = slotItem.Id;
                    CurrentFuelFluidId = FluidMaster.EmptyFluidId;
                    RemainingFuelTime = setting.ConsumptionTime;
                    return true;
                }
                
                return false;
            }
            
            // タンク内の流体燃料を消費して燃焼を開始する
            // Ignite combustion by consuming fuel from the internal fluid tank
            bool TryStartFluidFuel()
            {
                if (_fluidFuelSettings.Count == 0) return false;
                
                var tank = _fluidComponent.SteamTank;
                var fluidId = tank.FluidId;
                if (!_fluidFuelSettings.TryGetValue(fluidId, out var setting)) return false;
                if (tank.Amount < setting.Amount) return false;
                
                tank.Amount -= setting.Amount;
                if (tank.Amount <= 0)
                {
                    tank.Amount = 0;
                    tank.FluidId = FluidMaster.EmptyFluidId;
                }
                
                CurrentFuelType = FuelType.Fluid;
                CurrentFuelFluidId = fluidId;
                CurrentFuelItemId = ItemMaster.EmptyItemId;
                RemainingFuelTime = setting.ConsumptionTime;
                return true;
            }
            
            #endregion
        }

        public void Update(float operatingRate)
        {
            if (!IsFuelActive) return;

            RemainingFuelTime -= GameUpdater.UpdateSecondTime * operatingRate;
            if (RemainingFuelTime > 0) return;

            ClearFuelState();
        }

        // セーブ時に保持した燃料状態を復元する
        // Restore the fuel state saved during serialization
        public void Restore(FuelGearGeneratorSaveData saveData)
        {
            RemainingFuelTime = saveData.RemainingFuelTime;
            CurrentFuelType = Enum.TryParse(saveData.ActiveFuelType, out FuelType parsed) ? parsed : FuelType.None;
            CurrentFuelItemId = saveData.CurrentFuelItemGuid.HasValue
                ? MasterHolder.ItemMaster.GetItemId(saveData.CurrentFuelItemGuid.Value)
                : ItemMaster.EmptyItemId;
            CurrentFuelFluidId = saveData.CurrentFuelFluidGuid.HasValue
                ? MasterHolder.FluidMaster.GetFluidId(saveData.CurrentFuelFluidGuid.Value)
                : FluidMaster.EmptyFluidId;

            // 必要に応じて燃料状態のクリアを行う
            // Clear fuel state as needed
            ClearRestore();
            
            #region Internal
            
            void ClearRestore()
            {
                if (RemainingFuelTime <= 0)
                {
                    ClearFuelState();
                    return;
                }
                
                // マスターが変わった時に不整合が生じるので、アイテムが存在しない場合は燃料状態をクリアする
                // To prevent inconsistencies when masters change, clear fuel state if the item no longer exists
                if (CurrentFuelType == FuelType.Item && !_itemFuelSettings.ContainsKey(CurrentFuelItemId))
                {
                    ClearFuelState();
                    return;
                }
                
                if (CurrentFuelType == FuelType.Fluid && !_fluidFuelSettings.ContainsKey(CurrentFuelFluidId))
                {
                    ClearFuelState();
                } 
            }
            
            #endregion
        }

        private void ClearFuelState()
        {
            CurrentFuelType = FuelType.None;
            CurrentFuelItemId = ItemMaster.EmptyItemId;
            CurrentFuelFluidId = FluidMaster.EmptyFluidId;
            RemainingFuelTime = 0;
        }

        private readonly struct ItemFuelSetting
        {
            public int Count { get; }
            public double ConsumptionTime { get; }
            
            public ItemFuelSetting(int count, double consumptionTime)
            {
                Count = count;
                ConsumptionTime = consumptionTime;
            }
        }

        private readonly struct FluidFuelSetting
        {
            public double Amount { get; }
            public double ConsumptionTime { get; }
            public FluidFuelSetting(double amount, double consumptionTime)
            {
                Amount = amount;
                ConsumptionTime = consumptionTime;
            }

        }
    }
}
