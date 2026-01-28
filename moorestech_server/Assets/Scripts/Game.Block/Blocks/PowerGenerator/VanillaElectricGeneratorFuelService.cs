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

        private readonly Dictionary<ItemId, (FuelItemsElement Setting, uint Ticks)> _itemFuelSettings;
        private readonly Dictionary<FluidId, (FuelFluidsElement Setting, uint Ticks)> _fluidFuelSettings;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer _fuelFluidContainer;

        private ItemId _currentFuelItemId = ItemMaster.EmptyItemId;
        private FluidId _currentFuelFluidId = FluidMaster.EmptyFluidId;
        private FuelType _currentFuelType = FuelType.None;
        private uint _remainingFuelTicks;

        public VanillaElectricGeneratorFuelService(ElectricGeneratorBlockParam param, OpenableInventoryItemDataStoreService itemDataStoreService)
        {
            _itemDataStoreService = itemDataStoreService;
            _itemFuelSettings = BuildItemFuelSettings(param);
            _fluidFuelSettings = BuildFluidFuelSettings(param);

            // マスターで液体燃料が設定されている場合のみタンクを生成し、無い場合はダミーで済ませる。
            // Only create a tank if liquid fuel is configured in the master; otherwise, leave it null.
            var hasFluidTank = _fluidFuelSettings.Count > 0 && param.FuelFluidTankCapacity > 0;
            _fuelFluidContainer = hasFluidTank ? new FluidContainer(param.FuelFluidTankCapacity) : null;

            #region Internal

            Dictionary<ItemId, (FuelItemsElement, uint)> BuildItemFuelSettings(ElectricGeneratorBlockParam generatorParam)
            {
                var settings = new Dictionary<ItemId, (FuelItemsElement, uint)>();
                if (generatorParam.FuelItems == null) return settings;

                foreach (var fuelItem in generatorParam.FuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(fuelItem.ItemGuid);
                    var ticks = GameUpdater.SecondsToTicks(fuelItem.Time);
                    settings[itemId] = (fuelItem, ticks);
                }

                return settings;
            }

            Dictionary<FluidId, (FuelFluidsElement, uint)> BuildFluidFuelSettings(ElectricGeneratorBlockParam generatorParam)
            {
                var settings = new Dictionary<FluidId, (FuelFluidsElement, uint)>();
                if (generatorParam.FuelFluids == null) return settings;

                foreach (var fuelFluid in generatorParam.FuelFluids)
                {
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(fuelFluid.FluidGuid);
                    var ticks = GameUpdater.SecondsToTicks(fuelFluid.Time);
                    settings[fluidId] = (fuelFluid, ticks);
                }

                return settings;
            }

            #endregion
        }

        public ElectricPower GetOutputEnergy()
        {
            if (_currentFuelType == FuelType.Item && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var itemFuel))
            {
                return (ElectricPower)itemFuel.Setting.Power;
            }

            if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidFuel))
            {
                return (ElectricPower)fluidFuel.Setting.Power;
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
                if (_itemFuelSettings.Count == 0) return false;

                for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
                {
                    var slotItem = _itemDataStoreService.InventoryItems[i];
                    var slotItemId = slotItem.Id;
                    if (!_itemFuelSettings.TryGetValue(slotItemId, out var fuel)) continue;

                    if (slotItem.Count < 1) continue;

                    _currentFuelItemId = slotItemId;
                    _currentFuelFluidId = FluidMaster.EmptyFluidId;
                    _currentFuelType = FuelType.Item;
                    _remainingFuelTicks = fuel.Ticks;

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
                if (!_fluidFuelSettings.TryGetValue(fluidId, out var fuel)) return;
                if (fuel.Setting.Amount <= 0 || _fuelFluidContainer.Amount < fuel.Setting.Amount) return;

                _fuelFluidContainer.Amount -= fuel.Setting.Amount;
                if (_fuelFluidContainer.Amount <= 0)
                {
                    _fuelFluidContainer.Amount = 0;
                    _fuelFluidContainer.FluidId = FluidMaster.EmptyFluidId;
                }

                _currentFuelItemId = ItemMaster.EmptyItemId;
                _currentFuelFluidId = fluidId;
                _currentFuelType = FuelType.Fluid;
                _remainingFuelTicks = fuel.Ticks;
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
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            saveData.RemainingFuelSeconds = GameUpdater.TicksToSeconds(_remainingFuelTicks);
            saveData.ActiveFuelType = _currentFuelType.ToString();

            saveData.CurrentFuelItemGuidStr = null;
            if (_currentFuelType == FuelType.Item && _currentFuelItemId != ItemMaster.EmptyItemId && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var itemFuel))
            {
                saveData.CurrentFuelItemGuidStr = itemFuel.Setting.ItemGuid.ToString();
            }

            saveData.CurrentFuelFluidGuidStr = null;
            if (_currentFuelType == FuelType.Fluid && _currentFuelFluidId != FluidMaster.EmptyFluidId && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidFuel))
            {
                saveData.CurrentFuelFluidGuidStr = fluidFuel.Setting.FluidGuid.ToString();
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

            // 秒数からtickに変換して復元
            // Convert seconds back to ticks for restoration
            _remainingFuelTicks = GameUpdater.SecondsToTicks(saveData.RemainingFuelSeconds);

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
