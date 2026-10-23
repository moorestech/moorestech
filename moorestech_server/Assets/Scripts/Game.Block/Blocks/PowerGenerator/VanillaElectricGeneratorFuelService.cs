using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Interface.State;
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
        internal enum FuelType
        {
            None,
            Item,
            Fluid,
        }

        internal readonly Dictionary<ItemId, (FuelItemsElement Setting, uint Ticks)> _itemFuelSettings;
        internal readonly Dictionary<FluidId, (FuelFluidsElement Setting, uint Ticks)> _fluidFuelSettings;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        internal readonly FluidContainer _fuelFluidContainer;

        internal ItemId _currentFuelItemId = ItemMaster.EmptyItemId;
        internal FluidId _currentFuelFluidId = FluidMaster.EmptyFluidId;
        internal FuelType _currentFuelType = FuelType.None;
        internal uint RemainingFuelTicks { get; private set; }

        public VanillaElectricGeneratorFuelService(ElectricGeneratorBlockParam param, OpenableInventoryItemDataStoreService itemDataStoreService)
        {
            _itemDataStoreService = itemDataStoreService;
            _itemFuelSettings = VanillaElectricGeneratorFuelSettingsBuilder.BuildItemFuelSettings(param);
            _fluidFuelSettings = VanillaElectricGeneratorFuelSettingsBuilder.BuildFluidFuelSettings(param);

            // 許可燃料が無くても容量のあるタンクは残液を保持
            // Keep residual fluid in tanks with capacity even without permitted fuel
            var hasFluidTank = 0 < param.FuelFluidTankCapacity;
            _fuelFluidContainer = hasFluidTank ? new FluidContainer(param.FuelFluidTankCapacity) : null;
        }

        public ElectricPower GetOutputEnergy()
        {
            if (_currentFuelType == FuelType.Item && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var itemFuel)) return (ElectricPower)itemFuel.Setting.Power;
            if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluidFuel)) return (ElectricPower)fluidFuel.Setting.Power;
            return new ElectricPower(0);
        }

        // 燃焼中か（表示用）
        // Whether a fuel unit is burning (for display)
        internal bool IsBurning => _currentFuelType != FuelType.None;

        // 燃焼中の燃料1単位の総tick。燃えていなければ0
        // Total ticks of the burning fuel unit; zero when nothing burns
        internal uint CurrentFuelTotalTicks
        {
            get
            {
                if (_currentFuelType == FuelType.Item && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var item)) return item.Ticks;
                if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluid)) return fluid.Ticks;
                return 0;
            }
        }

        // 表示用タンクを返す。容量が無い場合は持たない
        // Return the display tank; generators without capacity have none
        internal bool TryGetFuelTank(out FluidMessagePack tank)
        {
            tank = null;
            if (_fuelFluidContainer == null) return false;
            tank = new FluidMessagePack(_fuelFluidContainer.FluidId, _fuelFluidContainer.Amount, _fuelFluidContainer.Capacity);
            return true;
        }

        // 燃料の点き始め・燃え尽きのtickだけtrueを返す（状態通知の合図）
        // Returns true only on ticks where a fuel unit ignites or burns out (the cue for a state notification)
        public bool Update()
        {
            var wasBurning = IsBurning;
            // 1. タンク状態を整えてからタイマーや燃料選択を処理する。
            // 1. First, maintain the tank state before processing timers and fuel selection.
            MaintainFluidContainer();

            // 2. 稼働中の燃料を先に消費し、継続中なら以降の処理は不要。
            // 2. Consume the fuel in operation first, and if it continues, no further processing is required.
            if (_currentFuelType != FuelType.None)
            {
                TickFuelTimer();

            }

            // 3. 固体燃料を優先して次の燃料を探し、無ければ液体燃料を使用する。
            // 3. First look for the next fuel with solid fuel, and if not, use liquid fuel.
            if (!IsBurning && !TryStartItemFuel()) TryStartFluidFuel();
            var ignitedNewUnit = IsBurning && RemainingFuelTicks == CurrentFuelTotalTicks;
            return wasBurning != IsBurning || ignitedNewUnit;

            #region Internal

            void TickFuelTimer()
            {
                if (RemainingFuelTicks <= 1u) { ClearFuelState(); return; }

                RemainingFuelTicks--;
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
                    RemainingFuelTicks = fuel.Ticks;

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
                if (_fuelFluidContainer.Amount <= 0) { _fuelFluidContainer.Amount = 0; _fuelFluidContainer.FluidId = FluidMaster.EmptyFluidId; }

                _currentFuelItemId = ItemMaster.EmptyItemId;
                _currentFuelFluidId = fluidId;
                _currentFuelType = FuelType.Fluid;
                RemainingFuelTicks = fuel.Ticks;
            }

            void MaintainFluidContainer()
            {
                if (_fuelFluidContainer == null) return;

                if (_fuelFluidContainer.Amount <= 0) { _fuelFluidContainer.Amount = 0; _fuelFluidContainer.FluidId = FluidMaster.EmptyFluidId; }
            }

            void ClearFuelState()
            {
                _currentFuelItemId = ItemMaster.EmptyItemId;
                _currentFuelFluidId = FluidMaster.EmptyFluidId;
                _currentFuelType = FuelType.None;
                RemainingFuelTicks = 0;
            }

            #endregion
        }

        public FluidStack AddLiquid(FluidStack fluidStack)
        {
            // タンク容量が無い場合は全量返す
            // Return the full stack when no tank capacity exists
            if (_fuelFluidContainer == null) return fluidStack;
            return _fuelFluidContainer.AddLiquid(fluidStack).Remainder;
        }

        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fuelFluidContainer != null && 0 < _fuelFluidContainer.Amount) fluidStacks.Add(new FluidStack(_fuelFluidContainer.Amount, _fuelFluidContainer.FluidId));
            return fluidStacks;
        }

        public void WriteSaveData(VanillaElectricGeneratorSaveJsonObject saveData) => VanillaElectricGeneratorFuelSaveConverter.Write(this, saveData);

        public void Restore(VanillaElectricGeneratorSaveJsonObject saveData)
        {
            VanillaElectricGeneratorFuelSaveConverter.Restore(this, saveData);
            RemainingFuelTicks = GameUpdater.SecondsToTicks(saveData.RemainingFuelSeconds);
        }
    }
}
