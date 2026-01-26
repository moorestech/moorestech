using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.PowerGenerator
{
    /// <summary>
    /// 発電機の燃料管理を一手に担い、コンポーネント本体から責務を切り離すサービス。
    /// A service that takes full responsibility for generator fuel management, separating responsibility from the component itself.
    /// </summary>
    public class VanillaElectricGeneratorFuelService
    {
        private enum FuelType
        {
            None,
            Item,
            Fluid,
        }

        private readonly Dictionary<ItemId, FuelItemsElement> _fuelSettings;
        private readonly Dictionary<FluidId, FuelFluidsElement> _fluidFuelSettings;
        private readonly Dictionary<ItemId, uint> _itemFuelTicks;
        private readonly Dictionary<FluidId, uint> _fluidFuelTicks;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer _fuelFluidContainer;

        private ItemId _currentFuelItemId = ItemMaster.EmptyItemId;
        private FluidId _currentFuelFluidId = FluidMaster.EmptyFluidId;
        private FuelType _currentFuelType = FuelType.None;
        private uint _remainingFuelTicks;

        public VanillaElectricGeneratorFuelService(ElectricGeneratorBlockParam param, OpenableInventoryItemDataStoreService itemDataStoreService)
        {
            _itemDataStoreService = itemDataStoreService;
            _fuelSettings = BuildFuelSettings(param);
            _fluidFuelSettings = BuildFluidFuelSettings(param);
            _itemFuelTicks = BuildItemFuelTicks(param);
            _fluidFuelTicks = BuildFluidFuelTicks(param);

            // マスターで液体燃料が設定されている場合のみタンクを生成し、無い場合はダミーで済ませる。
            var hasFluidTank = _fluidFuelSettings.Count > 0 && param.FuelFluidTankCapacity > 0;
            _fuelFluidContainer = hasFluidTank ? new FluidContainer(param.FuelFluidTankCapacity) : null;

            #region Internal

            Dictionary<ItemId, FuelItemsElement> BuildFuelSettings(ElectricGeneratorBlockParam generatorParam)
            {
                var settings = new Dictionary<ItemId, FuelItemsElement>();
                if (generatorParam.FuelItems == null) return settings;

                foreach (var fuelItem in generatorParam.FuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(fuelItem.ItemGuid);
                    settings[itemId] = fuelItem;
                }

                return settings;
            }

            Dictionary<FluidId, FuelFluidsElement> BuildFluidFuelSettings(ElectricGeneratorBlockParam generatorParam)
            {
                var settings = new Dictionary<FluidId, FuelFluidsElement>();
                if (generatorParam.FuelFluids == null) return settings;

                foreach (var fuelFluid in generatorParam.FuelFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(fuelFluid.FluidGuid);
                    settings[fluidId] = fuelFluid;
                }

                return settings;
            }

            Dictionary<ItemId, uint> BuildItemFuelTicks(ElectricGeneratorBlockParam generatorParam)
            {
                var tickMap = new Dictionary<ItemId, uint>();
                if (generatorParam.FuelItems == null) return tickMap;

                foreach (var fuelItem in generatorParam.FuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(fuelItem.ItemGuid);
                    tickMap[itemId] = GameUpdater.SecondsToTicks(fuelItem.Time);
                }

                return tickMap;
            }

            Dictionary<FluidId, uint> BuildFluidFuelTicks(ElectricGeneratorBlockParam generatorParam)
            {
                var tickMap = new Dictionary<FluidId, uint>();
                if (generatorParam.FuelFluids == null) return tickMap;

                foreach (var fuelFluid in generatorParam.FuelFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(fuelFluid.FluidGuid);
                    tickMap[fluidId] = GameUpdater.SecondsToTicks(fuelFluid.Time);
                }

                return tickMap;
            }

            #endregion
        }

        public ElectricPower GetOutputEnergy()
        {
            if (_currentFuelType == FuelType.Item && _fuelSettings.TryGetValue(_currentFuelItemId, out var itemFuel))
            {
                return (ElectricPower)itemFuel.Power;
            }

            if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidFuel))
            {
                return (ElectricPower)fluidFuel.Power;
            }

            return new ElectricPower(0);
        }

        public void Update()
        {
            // 1. タンク状態を整えてからタイマーや燃料選択を処理する。
            // 1. First, maintain the tank state before processing timers and fuel selection.
            MaintainFluidContainer();

            if (_currentFuelType != FuelType.None)
            {
                // 2. 稼働中の燃料を先に消費し、継続中なら以降の処理は不要。
                // 2. Consume the fuel in operation first, and if it continues, no further processing is required.
                TickFuelTimer();
                
                // 燃料消費中なら以降の処理は不要
                // If fuel is being consumed, no further processing is required.
                if (_currentFuelType != FuelType.None) return;
            }

            // 3. 固体燃料を優先して次の燃料を探し、無ければ液体燃料を使用する。
            // 3. First look for the next fuel with solid fuel, and if not, use liquid fuel.
            if (TryStartItemFuel()) return;

            TryStartFluidFuel();

            #region Internal

            void TickFuelTimer()
            {
                var ticksToConsume = GameUpdater.CurrentTickCount;
                if (ticksToConsume >= _remainingFuelTicks)
                {
                    ClearFuelState();
                    return;
                }

                _remainingFuelTicks -= ticksToConsume;
            }

            bool TryStartItemFuel()
            {
                if (_fuelSettings.Count == 0) return false;

                for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
                {
                    var slotItem = _itemDataStoreService.InventoryItems[i];
                    var slotItemId = slotItem.Id;
                    if (!_itemFuelTicks.TryGetValue(slotItemId, out var fuelTicks)) continue;

                    if (slotItem.Count < 1) continue;

                    _currentFuelItemId = slotItemId;
                    _currentFuelFluidId = FluidMaster.EmptyFluidId;
                    _currentFuelType = FuelType.Item;
                    _remainingFuelTicks = fuelTicks;

                    _itemDataStoreService.SetItem(i, slotItem.SubItem(1));
                    return true;
                }

                return false;
            }

            void TryStartFluidFuel()
            {
                if (_fuelFluidContainer == null) return;

                var fluidId = _fuelFluidContainer.FluidId;
                if (fluidId == FluidMaster.EmptyFluidId) return;
                if (!_fluidFuelSettings.TryGetValue(fluidId, out var fluidSetting)) return;
                if (!_fluidFuelTicks.TryGetValue(fluidId, out var fuelTicks)) return;
                if (fluidSetting.Amount <= 0 || _fuelFluidContainer.Amount < fluidSetting.Amount) return;

                _fuelFluidContainer.Amount -= fluidSetting.Amount;
                if (_fuelFluidContainer.Amount <= 0)
                {
                    _fuelFluidContainer.Amount = 0;
                    _fuelFluidContainer.FluidId = FluidMaster.EmptyFluidId;
                }

                _currentFuelItemId = ItemMaster.EmptyItemId;
                _currentFuelFluidId = fluidId;
                _currentFuelType = FuelType.Fluid;
                _remainingFuelTicks = fuelTicks;
            }

            void MaintainFluidContainer()
            {
                if (_fuelFluidContainer == null) return;

                if (_fuelFluidContainer.Amount <= 0)
                {
                    _fuelFluidContainer.Amount = 0;
                    _fuelFluidContainer.FluidId = FluidMaster.EmptyFluidId;
                }

                _fuelFluidContainer.PreviousSourceFluidContainers.Clear();
            }

            void ClearFuelState()
            {
                _currentFuelItemId = ItemMaster.EmptyItemId;
                _currentFuelFluidId = FluidMaster.EmptyFluidId;
                _currentFuelType = FuelType.None;
                _remainingFuelTicks = 0;
            }

            #endregion
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        { 
            return _fuelFluidContainer.AddLiquid(fluidStack, source);
        }

        
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fuelFluidContainer.Amount > 0)
            {
                fluidStacks.Add(new FluidStack(_fuelFluidContainer.Amount, _fuelFluidContainer.FluidId));
            }
            return fluidStacks;
        }

        public void WriteSaveData(VanillaElectricGeneratorSaveJsonObject saveData)
        {
            // 現在の燃焼状況と残量を記録し、セーブ・ロード後に同じ状態を再現できるようにする。
            // Record the current combustion status and remaining amount so that the same state can be reproduced after saving and loading.
            saveData.RemainingFuelTicks = _remainingFuelTicks;
            saveData.ActiveFuelType = _currentFuelType.ToString();

            saveData.CurrentFuelItemGuidStr = null;
            if (_currentFuelType == FuelType.Item && _currentFuelItemId != ItemMaster.EmptyItemId && _fuelSettings.TryGetValue(_currentFuelItemId, out var itemFuel))
            {
                saveData.CurrentFuelItemGuidStr = itemFuel.ItemGuid.ToString();
            }

            saveData.CurrentFuelFluidGuidStr = null;
            if (_currentFuelType == FuelType.Fluid && _currentFuelFluidId != FluidMaster.EmptyFluidId && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidFuel))
            {
                saveData.CurrentFuelFluidGuidStr = fluidFuel.FluidGuid.ToString();
            }
            
            saveData.FluidTank = null;
            if (_fuelFluidContainer != null)
            {
                var fluidGuid = MasterHolder.FluidMaster.GetFluidMaster(_fuelFluidContainer.FluidId).FluidGuid;
                saveData.FluidTank = new VanillaElectricGeneratorFluidSaveJsonObject
                {
                    FluidGuidStr = fluidGuid.ToString(),
                    Amount = _fuelFluidContainer.Amount,
                };
            }
        }

        public void Restore(VanillaElectricGeneratorSaveJsonObject saveData)
        {
            if (saveData == null) return;

            // 保存データから燃焼状態とタンクを復元し、液体燃料が無効ならアイドルへ戻す。
            // Restore the combustion state and tank from the saved data, and return to idle if liquid fuel is disabled.
            _remainingFuelTicks = saveData.RemainingFuelTicks;

            if (!string.IsNullOrEmpty(saveData.ActiveFuelType) && Enum.TryParse(saveData.ActiveFuelType, out FuelType parsedType))
            {
                _currentFuelType = parsedType;
            }
            else
            {
                _currentFuelType = FuelType.None;
            }

            _currentFuelItemId = saveData.CurrentFuelItemGuid.HasValue
                ? MasterHolder.ItemMaster.GetItemId(saveData.CurrentFuelItemGuid.Value)
                : ItemMaster.EmptyItemId;

            _currentFuelFluidId = saveData.CurrentFuelFluidGuid.HasValue
                ? MasterHolder.FluidMaster.GetFluidId(saveData.CurrentFuelFluidGuid.Value)
                : FluidMaster.EmptyFluidId;

            if (saveData.FluidTank != null)
            {
                var savedFluidGuid = saveData.FluidTank.FluidGuid;
                _fuelFluidContainer.FluidId = savedFluidGuid.HasValue
                    ? MasterHolder.FluidMaster.GetFluidId(savedFluidGuid.Value)
                    : FluidMaster.EmptyFluidId;
                _fuelFluidContainer.Amount = saveData.FluidTank.Amount;
            }

        }

    }
}
