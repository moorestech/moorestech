using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// SteamGearGenerator専用の流体入力コンポーネント
    /// パイプからの蒸気入力のみを扱う
    /// </summary>
    public class SteamGearGeneratorFluidComponent : IFluidInventory, IUpdatableBlockComponent
    {
        private readonly FluidContainer _steamTank;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        
        public SteamGearGeneratorFluidComponent(float tankCapacity, BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _steamTank = new FluidContainer(tankCapacity);
            _fluidConnector = fluidConnector;
        }
        
        public FluidContainer SteamTank => _steamTank;
        
        public void Update()
        {
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
    }
}