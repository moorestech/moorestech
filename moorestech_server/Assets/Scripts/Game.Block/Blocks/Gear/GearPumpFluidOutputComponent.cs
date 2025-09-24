using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// Holds an inner fluid tank and pushes it to connected pipes each update.
    /// Output-only; AddLiquid simply returns the input unchanged.
    /// </summary>
    public class GearPumpFluidOutputComponent : IFluidInventory, IUpdatableBlockComponent
    {
        private readonly FluidContainer _tank;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;

        public GearPumpFluidOutputComponent(float capacity, BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _tank = new FluidContainer(capacity);
            _fluidConnector = fluidConnector;
        }

        public FluidContainer Tank => _tank;

        public void Update()
        {
            // Push fluid to connected inventories
            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                if (_tank.Amount <= 0) break;

                var flowRate = GetFlowRate(info);
                var transferAmount = System.Math.Min(_tank.Amount, flowRate * 1.0); // simple per-tick flow
                if (transferAmount <= 0) continue;

                var stack = new FluidStack(transferAmount, _tank.FluidId);
                var remaining = inventory.AddLiquid(stack, _tank);
                var transferred = transferAmount - remaining.Amount;
                if (transferred > 0)
                {
                    _tank.Amount -= transferred;
                    if (_tank.Amount <= 0)
                    {
                        _tank.Amount = 0;
                        _tank.FluidId = FluidMaster.EmptyFluidId;
                    }
                }
            }

            // maintenance
            _tank.PreviousSourceFluidContainers.Clear();
            if (_tank.Amount <= 0)
            {
                _tank.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        private static double GetFlowRate(ConnectedInfo info)
        {
            if (info.SelfOption is FluidConnectOption option)
            {
                return option.FlowCapacity;
            }
            return 10.0;
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // Output only; do not accept external input
            return fluidStack;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        public List<FluidStack> GetFluidInventory()
        {
            var list = new List<FluidStack>();
            if (_tank.Amount > 0)
            {
                list.Add(new FluidStack(_tank.Amount, _tank.FluidId));
            }
            return list;
        }
    }
}
