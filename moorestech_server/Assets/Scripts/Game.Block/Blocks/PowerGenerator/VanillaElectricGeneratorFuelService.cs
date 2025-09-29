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
    ///     バニラ発電機の燃料管理を一手に担い、コンポーネント本体から責務を切り離すサービス。
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
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer _fuelFluidContainer;

        private ItemId _currentFuelItemId = ItemMaster.EmptyItemId;
        private FluidId _currentFuelFluidId = FluidMaster.EmptyFluidId;
        private FuelType _currentFuelType = FuelType.None;
        private double _remainingFuelTime;

        public VanillaElectricGeneratorFuelService(
            Dictionary<ItemId, FuelItemsElement> fuelSettings,
            Dictionary<FluidId, FuelFluidsElement> fluidFuelSettings,
            OpenableInventoryItemDataStoreService itemDataStoreService,
            double fuelFluidTankCapacity)
        {
            _fuelSettings = fuelSettings ?? new Dictionary<ItemId, FuelItemsElement>();
            _fluidFuelSettings = fluidFuelSettings ?? new Dictionary<FluidId, FuelFluidsElement>();
            _itemDataStoreService = itemDataStoreService;
            // マスターで液体燃料が設定されている場合のみタンクを生成し、無い場合はダミーで済ませる。
            var hasFluidTank = _fluidFuelSettings.Count > 0 && fuelFluidTankCapacity > 0;
            _fuelFluidContainer = hasFluidTank ? new FluidContainer(fuelFluidTankCapacity) : null;
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
            MaintainFluidContainer();

            if (_currentFuelType != FuelType.None)
            {
                // 2. 稼働中の燃料を先に消費し、継続中なら以降の処理は不要。
                TickFuelTimer();
                if (_currentFuelType != FuelType.None) return;
            }

            // 3. 固体燃料を優先して次の燃料を探し、無ければ液体燃料を使用する。
            if (TryStartItemFuel()) return;

            TryStartFluidFuel();

            #region Internal

            void TickFuelTimer()
            {
                _remainingFuelTime -= GameUpdater.UpdateSecondTime;
                if (_remainingFuelTime > 0) return;

                ClearFuelState();
            }

            bool TryStartItemFuel()
            {
                if (_fuelSettings.Count == 0) return false;

                for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
                {
                    var slotItem = _itemDataStoreService.InventoryItems[i];
                    var slotItemId = slotItem.Id;
                    if (!_fuelSettings.TryGetValue(slotItemId, out var itemSetting)) continue;

                    _currentFuelItemId = slotItemId;
                    _currentFuelFluidId = FluidMaster.EmptyFluidId;
                    _currentFuelType = FuelType.Item;
                    _remainingFuelTime = itemSetting.Time;

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
                _remainingFuelTime = fluidSetting.Time;
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
                _remainingFuelTime = 0;
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

        public void WritSaveData(VanillaElectricGeneratorSaveJsonObject saveData)
        {
            // 現在の燃焼状況と残量を記録し、セーブ・ロード後に同じ状態を再現できるようにする。
            saveData.RemainingFuelTime = _remainingFuelTime;
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
            _remainingFuelTime = saveData.RemainingFuelTime;

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
