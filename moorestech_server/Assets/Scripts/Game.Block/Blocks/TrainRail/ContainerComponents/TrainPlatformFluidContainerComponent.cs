using System.Collections.Generic;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    /// <summary>
    /// 液体貨物プラットフォームのコンテナコンポーネント（スタブ）
    /// Fluid cargo platform container component (stub)
    /// </summary>
    public class TrainPlatformFluidContainerComponent : IBlockComponent, IFluidInventory
    {
        public bool IsDestroy { get; private set; }
        private readonly TrainPlatformDockingComponent _dockingComponent;
        private readonly TrainPlatformTransferComponent _transferComponent;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        private readonly double _capacity;

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _capacity = capacity;
            _fluidConnector = fluidConnector;
        }

        public TrainPlatformFluidContainerComponent(
            TrainPlatformDockingComponent dockingComponent,
            TrainPlatformTransferComponent transferComponent,
            double capacity,
            BlockConnectorComponent<IFluidInventory> fluidConnector,
            Dictionary<string, string> componentStates)
        {
            _dockingComponent = dockingComponent;
            _transferComponent = transferComponent;
            _capacity = capacity;
            _fluidConnector = fluidConnector;
            // TODO セーブデータからの復元処理
            // TODO Restore from save data
        }

        public List<FluidStack> GetFluidInventory()
        {
            // TODO 液体インベントリの取得処理
            // TODO Return fluid inventory
            return new List<FluidStack>();
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // TODO 液体追加処理
            // TODO Add liquid to container
            return fluidStack;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
