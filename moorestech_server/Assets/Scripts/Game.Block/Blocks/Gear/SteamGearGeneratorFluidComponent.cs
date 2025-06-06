using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface.Component;
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// SteamGearGenerator専用の流体入力コンポーネント
    /// パイプからの蒸気入力のみを扱う
    /// </summary>
    public class SteamGearGeneratorFluidComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        private readonly FluidContainer _steamTank;
        private bool _wasRefilledThisUpdate;
        private int _consecutiveUpdatesWithoutRefill;
        private bool _wasEverDisconnected;
        
        public SteamGearGeneratorFluidComponent(float tankCapacity)
        {
            _steamTank = new FluidContainer(tankCapacity);
            _wasRefilledThisUpdate = false;
            _consecutiveUpdatesWithoutRefill = 0;
            _wasEverDisconnected = false;
        }
        
        public SteamGearGeneratorFluidComponent(Dictionary<string, string> componentStates, float tankCapacity) : this(tankCapacity)
        {
            if (!componentStates.TryGetValue(SaveKey, out var saveState)) return;
            
            var saveData = JsonConvert.DeserializeObject<SteamGearGeneratorFluidSaveData>(saveState);
            
            _steamTank.FluidId = new FluidId(saveData.FluidId);
            _steamTank.Amount = saveData.Amount;
            _wasRefilledThisUpdate = saveData.WasRefilledThisUpdate;
            _consecutiveUpdatesWithoutRefill = saveData.ConsecutiveUpdatesWithoutRefill;
            _wasEverDisconnected = saveData.WasEverDisconnected;
        }
        
        public FluidContainer SteamTank => _steamTank;
        public bool IsPipeDisconnected => _wasEverDisconnected || _consecutiveUpdatesWithoutRefill >= 1;
        
        public void Update()
        {
            // この更新サイクルで補給があったかどうかをチェック
            _wasRefilledThisUpdate = _steamTank.PreviousSourceFluidContainers.Count > 0;
            
            // 補給状態を追跡
            if (_wasRefilledThisUpdate)
            {
                _consecutiveUpdatesWithoutRefill = 0;
            }
            else
            {
                _consecutiveUpdatesWithoutRefill++;
                // 一度でも切断が検知されたらフラグを立てる
                if (_consecutiveUpdatesWithoutRefill >= 1)
                {
                    _wasEverDisconnected = true;
                }
            }
            
            // タンクのPreviousSourceFluidContainersをクリア
            _steamTank.PreviousSourceFluidContainers.Clear();
            
            // タンクが空の場合はFluidIdをリセット
            if (_steamTank.Amount <= 0)
            {
                _steamTank.FluidId = FluidMaster.EmptyFluidId;
            }
        }
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // タンクに液体を追加
            return _steamTank.AddLiquid(fluidStack, source);
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        #region IBlockSaveState
        
        public string SaveKey => "steamGearGeneratorFluid";
        
        public string GetSaveState()
        {
            var saveData = new SteamGearGeneratorFluidSaveData
            {
                FluidId = _steamTank.FluidId.AsPrimitive(),
                Amount = _steamTank.Amount,
                WasRefilledThisUpdate = _wasRefilledThisUpdate,
                ConsecutiveUpdatesWithoutRefill = _consecutiveUpdatesWithoutRefill,
                WasEverDisconnected = _wasEverDisconnected
            };
            
            return JsonConvert.SerializeObject(saveData);
        }
        
        // Save data structure
        private class SteamGearGeneratorFluidSaveData
        {
            public int FluidId { get; set; }
            public double Amount { get; set; }
            public bool WasRefilledThisUpdate { get; set; }
            public int ConsecutiveUpdatesWithoutRefill { get; set; }
            public bool WasEverDisconnected { get; set; }
        }
        
        #endregion
    }
}