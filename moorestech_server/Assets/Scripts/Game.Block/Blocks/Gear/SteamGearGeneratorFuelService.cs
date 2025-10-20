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

        public FuelState CreateStateSnapshot()
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
                if (!_itemFuelSettings.TryGetValue(slotItem.Id, out var itemSetting)) continue;
                if (slotItem.Count < itemSetting.Amount) continue;

                var remainder = slotItem.SubItem(itemSetting.Amount);
                _inventoryService.SetItem(i, remainder);

                _currentFuelType = FuelType.Item;
                _currentFuelItemId = slotItem.Id;
                _currentFuelFluidId = FluidMaster.EmptyFluidId;
                _remainingFuelTime = NormalizeFuelTime(itemSetting.ConsumptionTime);
                return true;
            }

            return false;
        }

        private bool TryStartFluidFuel()
        {
            if (_fluidFuelSettings.Count == 0) return false;

            var steamTank = _fluidComponent.SteamTank;
            var currentFluidId = steamTank.FluidId;
            if (!_fluidFuelSettings.TryGetValue(currentFluidId, out var fluidSetting)) return false;
            if (steamTank.Amount < fluidSetting.Amount) return false;

            steamTank.Amount -= fluidSetting.Amount;
            if (steamTank.Amount <= 0)
            {
                steamTank.Amount = 0;
                steamTank.FluidId = FluidMaster.EmptyFluidId;
            }

            _currentFuelType = FuelType.Fluid;
            _currentFuelFluidId = currentFluidId;
            _currentFuelItemId = ItemMaster.EmptyItemId;
            _remainingFuelTime = NormalizeFuelTime(fluidSetting.ConsumptionTime);
            return true;
        }

        private bool InventoryHasFuel()
        {
            if (_itemFuelSettings.Count == 0) return false;

            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < slotSize; i++)
            {
                var slotItem = _inventoryService.GetItem(i);
                if (_itemFuelSettings.TryGetValue(slotItem.Id, out var itemSetting) && slotItem.Count >= itemSetting.Amount)
                {
                    return true;
                }
            }

            return false;
        }

        private bool FluidHasFuel()
        {
            if (_fluidFuelSettings.Count == 0) return false;

            var steamTank = _fluidComponent.SteamTank;
            if (!_fluidFuelSettings.TryGetValue(steamTank.FluidId, out var fluidSetting)) return false;
            return steamTank.Amount >= fluidSetting.Amount;
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

        private static Dictionary<ItemId, ItemFuelSetting> BuildItemFuelSettings(SteamGearGeneratorBlockParam param)
        {
            var settings = new Dictionary<ItemId, ItemFuelSetting>();
            if (param.ItemFuelConfig?.FuelEntries == null) return settings;

            foreach (var element in param.ItemFuelConfig.FuelEntries)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(element.ItemGuid);
                var amount = Math.Max(1, element.Amount);
                settings[itemId] = new ItemFuelSetting(element.ItemGuid, amount, element.ConsumptionTime);
            }

            return settings;
        }

        private static Dictionary<FluidId, FluidFuelSetting> BuildFluidFuelSettings(SteamGearGeneratorBlockParam param)
        {
            var settings = new Dictionary<FluidId, FluidFuelSetting>();
            if (param.RequiredFluids == null) return settings;

            foreach (var element in param.RequiredFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(element.FluidGuid);
                var amount = Math.Max(0d, element.Amount);
                settings[fluidId] = new FluidFuelSetting(element.FluidGuid, amount, element.ConsumptionTime);
            }

            return settings;
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
