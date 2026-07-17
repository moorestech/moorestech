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

        // このtick内にAddLiquidで補給を受けたかどうか（パイプ切断検知に使う）
        // Whether AddLiquid refilled the tank within this tick (used for pipe disconnection detection)
        private bool _refilledThisTick;

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
            if (_refilledThisTick)
            {
                _consecutiveUpdatesWithoutRefill = 0;
            }
            else
            {
                _consecutiveUpdatesWithoutRefill++;
            }
            _refilledThisTick = false;

            // タンクが空の場合はFluidIdをリセット
            // Reset the fluid id when the tank is empty
            if (_fuelTank.Amount <= 0)
            {
                _fuelTank.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            // タンクに液体を追加する。満タンで受け入れ0でも適合流体の供給が届いていれば補給ありとみなす（満タン時の誤切断防止）
            // Add liquid to the tank; even a zero-accepted delivery of a compatible fluid counts as a refill (prevents false disconnection while full)
            var result = _fuelTank.AddLiquid(fluidStack);
            if (result.IsCompatibleSupply) _refilledThisTick = true;
            return result.Remainder;
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
