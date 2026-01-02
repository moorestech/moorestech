using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidConnectOptionModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Holds an inner fluid tank and pushes it to connected pipes each update.
    /// Output-only for external inventories; internal generators enqueue via AddLiquid.
    /// </summary>
    public class PumpFluidOutputComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey  { get; }  = typeof(PumpFluidOutputComponent).FullName;
        
        private readonly FluidContainer _tank;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;

        public PumpFluidOutputComponent(float capacity, BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _tank = new FluidContainer(capacity);
            _fluidConnector = fluidConnector;
        }

        public PumpFluidOutputComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> fluidConnector) : this(capacity, fluidConnector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var state) || string.IsNullOrEmpty(state))
            {
                return;
            }

            var json = JsonConvert.DeserializeObject<PumpFluidOutputSaveJson>(state);
            var restoredAmount = Math.Min(json.Amount, _tank.Capacity);
            
            _tank.Amount = restoredAmount;
            _tank.FluidId = new FluidId(json.FluidIdValue);
        }

        public void Update()
        {
            // Push fluid to connected inventories
            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                if (_tank.Amount <= 0) break;

                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(_tank.Amount, flowRate * 1.0); // simple per-tick flow
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
            if (info.SelfConnector?.ConnectOption is FluidConnectOption option)
            {
                return option.FlowCapacity;
            }
            return 10.0;
        }
        
        public void EnqueueGeneratedFluid(FluidStack fluidStack)
        {
            _tank.AddLiquid(fluidStack, FluidContainer.Empty);
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // 外部からの注入は拒否する（供給専用）
            // Refuse external injections (supply only)
            return fluidStack;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);

            var state = new PumpFluidOutputSaveJson
            {
                FluidIdValue = _tank.FluidId.AsPrimitive(),
                Amount = _tank.Amount,
                Capacity = _tank.Capacity,
            };

            return JsonConvert.SerializeObject(state);
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
    
    public class PumpFluidOutputSaveJson
    {
        [JsonProperty("fluidId")]
        public int FluidIdValue;
        
        [JsonProperty("amount")]
        public double Amount;
        
        [JsonProperty("capacity")]
        public double Capacity;
    }
}
