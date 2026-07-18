using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface.Component;
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// FuelGearGenerator専用の流体入力コンポーネント
    /// パイプからの蒸気入力のみを扱う
    /// </summary>
    public class FuelGearGeneratorFluidComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        private readonly FluidContainer _fuelTank;
        private int _consecutiveUpdatesWithoutRefill;

        public FuelGearGeneratorFluidComponent(float tankCapacity)
        {
            _fuelTank = new FluidContainer(tankCapacity);
            _consecutiveUpdatesWithoutRefill = 0;
        }

        public FuelGearGeneratorFluidComponent(Dictionary<string, string> componentStates, float tankCapacity)
            : this(tankCapacity)
        {
            if (!componentStates.TryGetValue(SaveKey, out var saveState)) return;

            var saveData = JsonConvert.DeserializeObject<FuelGearGeneratorFluidSaveData>(saveState);
            _fuelTank = saveData.Fluid.ToFluidContainer(tankCapacity);

            _consecutiveUpdatesWithoutRefill = saveData.ConsecutiveUpdatesWithoutRefill;
        }
        
        public FluidContainer SteamTank => _fuelTank;
        public bool IsPipeDisconnected => _consecutiveUpdatesWithoutRefill >= 5;  // 5回連続で補給がない場合に切断とみなす
        
        public void Update()
        {
            // この更新サイクルで補給があったかどうかをチェック
            // Check whether the tank was refilled during this update cycle
            var wasRefilledThisUpdate = _fuelTank.HasPreviousSources;

            // 補給状態を追跡
            // Track the refill state
            if (wasRefilledThisUpdate)
            {
                _consecutiveUpdatesWithoutRefill = 0;
            }
            else
            {
                _consecutiveUpdatesWithoutRefill++;
            }
            
            // タンクの送信元記録をクリア
            _fuelTank.ClearPreviousSources();
            
            // タンクが空の場合はFluidIdをリセット
            if (_fuelTank.Amount <= 0)
            {
                _fuelTank.FluidId = FluidMaster.EmptyFluidId;
            }
        }
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // タンクに液体を追加
            return _fuelTank.AddLiquid(fluidStack, source);
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fuelTank.Amount > 0)
            {
                fluidStacks.Add(new FluidStack(_fuelTank.Amount, _fuelTank.FluidId));
            }
            return fluidStacks;
        }
        
        #region IBlockSaveState
        
        public string SaveKey => "fuelGearGeneratorFluid";
        
        public string GetSaveState()
        {
            var saveData = new FuelGearGeneratorFluidSaveData
            {
                Fluid = new FluidContainerSaveJsonObject(_fuelTank),
                ConsecutiveUpdatesWithoutRefill = _consecutiveUpdatesWithoutRefill
            };
            
            return JsonConvert.SerializeObject(saveData);
        }
        
        // Save data structure
        private class FuelGearGeneratorFluidSaveData
        {
            public FluidContainerSaveJsonObject Fluid { get; set; }
            public int ConsecutiveUpdatesWithoutRefill { get; set; }
        }
        
        #endregion
    }
}
