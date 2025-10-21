using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class SteamGearGeneratorFuelService
    {
        public class FuelState
        {
            public string ActiveFuelType { get; set; }
            public Guid? CurrentFuelItemGuid { get; set; }
            public Guid? CurrentFuelFluidGuid { get; set; }
            public double RemainingFuelTime { get; set; }
        }

        private enum FuelType
        {
            None,
            Item,
            Fluid
        }

        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;
        private readonly Dictionary<ItemId, ItemFuelSetting> _itemFuelSettings;
        private readonly Dictionary<FluidId, FluidFuelSetting> _fluidFuelSettings;

        private FuelType _currentFuelType;
        private ItemId _currentFuelItemId;
        private FluidId _currentFuelFluidId;
        private double _remainingFuelTime;

        public SteamGearGeneratorFuelService(
            SteamGearGeneratorBlockParam param,
            OpenableInventoryItemDataStoreService inventoryService,
            SteamGearGeneratorFluidComponent fluidComponent)
        {
            _inventoryService = inventoryService;
            _fluidComponent = fluidComponent;
            _itemFuelSettings = BuildItemFuelSettings(param);
            _fluidFuelSettings = BuildFluidFuelSettings(param);

            _currentFuelType = FuelType.None;
            _currentFuelItemId = ItemMaster.EmptyItemId;
            _currentFuelFluidId = FluidMaster.EmptyFluidId;
            _remainingFuelTime = 0;

            #region Internal

            static Dictionary<ItemId, ItemFuelSetting> BuildItemFuelSettings(SteamGearGeneratorBlockParam blockParam)
            {
                var settings = new Dictionary<ItemId, ItemFuelSetting>();
                if (blockParam.GearFuelItems == null) return settings;

                foreach (var element in blockParam.GearFuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(element.ItemGuid);
                    settings[itemId] = new ItemFuelSetting(element.ItemGuid, Math.Max(1, element.Amount), element.ConsumptionTime);
                }

                return settings;
            }

            static Dictionary<FluidId, FluidFuelSetting> BuildFluidFuelSettings(SteamGearGeneratorBlockParam blockParam)
            {
                var settings = new Dictionary<FluidId, FluidFuelSetting>();
                if (blockParam.RequiredFluids == null) return settings;

                foreach (var element in blockParam.RequiredFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(element.FluidGuid);
                    settings[fluidId] = new FluidFuelSetting(element.FluidGuid, Math.Max(0d, element.Amount), element.ConsumptionTime);
                }

                return settings;
            }

            #endregion
        }

        public bool IsFuelActive => _currentFuelType != FuelType.None && _remainingFuelTime > 0;
        public bool IsUsingFluidFuel => _currentFuelType == FuelType.Fluid;

        public bool HasAvailableFuel(bool allowFluidFuel)
        {
            if (IsFuelActive)
            {
                if (_currentFuelType == FuelType.Fluid && !allowFluidFuel) return false;
                return true;
            }

            if (InventoryHasFuel()) return true;
            return allowFluidFuel && FluidHasFuel();
        }

        public bool TryEnsureFuel(bool allowFluidFuel)
        {
            if (IsFuelActive)
            {
                if (_currentFuelType == FuelType.Fluid && !allowFluidFuel)
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
        }

        public void Update()
        {
            if (!IsFuelActive) return;

            _remainingFuelTime -= GameUpdater.UpdateSecondTime;
            if (_remainingFuelTime > 0) return;

            ClearFuelState();
        }

        public FuelState CreateSnapshot()
        {
            var state = new FuelState
            {
                ActiveFuelType = _currentFuelType.ToString(),
                RemainingFuelTime = _remainingFuelTime
            };

            if (_currentFuelType == FuelType.Item && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var itemSetting))
            {
                state.CurrentFuelItemGuid = itemSetting.ItemGuid;
            }

            if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidSetting))
            {
                state.CurrentFuelFluidGuid = fluidSetting.FluidGuid;
            }

            return state;
        }

        public void Restore(FuelState state)
        {
            ClearFuelState();
            if (state == null) return;

            _remainingFuelTime = state.RemainingFuelTime;

            if (!string.IsNullOrEmpty(state.ActiveFuelType) && Enum.TryParse(state.ActiveFuelType, out FuelType parsedType))
            {
                _currentFuelType = parsedType;
            }

            _currentFuelItemId = state.CurrentFuelItemGuid.HasValue
                ? MasterHolder.ItemMaster.GetItemId(state.CurrentFuelItemGuid.Value)
                : ItemMaster.EmptyItemId;

            _currentFuelFluidId = state.CurrentFuelFluidGuid.HasValue
                ? MasterHolder.FluidMaster.GetFluidId(state.CurrentFuelFluidGuid.Value)
                : FluidMaster.EmptyFluidId;

            if (_remainingFuelTime <= 0)
            {
                ClearFuelState();
                return;
            }

            if (_currentFuelType == FuelType.Item && !_itemFuelSettings.ContainsKey(_currentFuelItemId))
            {
                ClearFuelState();
                return;
            }

            if (_currentFuelType == FuelType.Fluid && !_fluidFuelSettings.ContainsKey(_currentFuelFluidId))
            {
                ClearFuelState();
            }
        }

        private bool TryStartItemFuel()
        {
            if (_itemFuelSettings.Count == 0) return false;

            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < slotSize; i++)
            {
                var slotItem = _inventoryService.GetItem(i);
                if (!_itemFuelSettings.TryGetValue(slotItem.Id, out var setting)) continue;
                if (slotItem.Count < setting.Amount) continue;

                var remainder = slotItem.SubItem(setting.Amount);
                _inventoryService.SetItem(i, remainder);

                _currentFuelType = FuelType.Item;
                _currentFuelItemId = slotItem.Id;
                _currentFuelFluidId = FluidMaster.EmptyFluidId;
                _remainingFuelTime = NormalizeFuelTime(setting.ConsumptionTime);
                return true;
            }

            return false;
        }

        private bool TryStartFluidFuel()
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

            _currentFuelType = FuelType.Fluid;
            _currentFuelFluidId = fluidId;
            _currentFuelItemId = ItemMaster.EmptyItemId;
            _remainingFuelTime = NormalizeFuelTime(setting.ConsumptionTime);
            return true;
        }

        private bool InventoryHasFuel()
        {
            if (_itemFuelSettings.Count == 0) return false;

            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < slotSize; i++)
            {
                var slotItem = _inventoryService.GetItem(i);
                if (_itemFuelSettings.TryGetValue(slotItem.Id, out var setting) && slotItem.Count >= setting.Amount)
                {
                    return true;
                }
            }

            return false;
        }

        private bool FluidHasFuel()
        {
            if (_fluidFuelSettings.Count == 0) return false;

            var tank = _fluidComponent.SteamTank;
            if (!_fluidFuelSettings.TryGetValue(tank.FluidId, out var setting)) return false;
            return tank.Amount >= setting.Amount;
        }

        private void ClearFuelState()
        {
            _currentFuelType = FuelType.None;
            _currentFuelItemId = ItemMaster.EmptyItemId;
            _currentFuelFluidId = FluidMaster.EmptyFluidId;
            _remainingFuelTime = 0;
        }

        private static double NormalizeFuelTime(double rawTime)
        {
            if (rawTime > 0) return rawTime;
            return GameUpdater.UpdateSecondTime > 0 ? GameUpdater.UpdateSecondTime : 0.1d;
        }

        private readonly struct ItemFuelSetting
        {
            public ItemFuelSetting(Guid itemGuid, int amount, double consumptionTime)
            {
                ItemGuid = itemGuid;
                Amount = amount;
                ConsumptionTime = consumptionTime;
            }

            public Guid ItemGuid { get; }
            public int Amount { get; }
            public double ConsumptionTime { get; }
        }

        private readonly struct FluidFuelSetting
        {
            public FluidFuelSetting(Guid fluidGuid, double amount, double consumptionTime)
            {
                FluidGuid = fluidGuid;
                Amount = amount;
                ConsumptionTime = consumptionTime;
            }

            public Guid FluidGuid { get; }
            public double Amount { get; }
            public double ConsumptionTime { get; }
        }
    }
}
