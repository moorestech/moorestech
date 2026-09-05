using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Newtonsoft.Json;
using UniRx;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Holds an inner fluid tank and pushes it to connected pipes each update.
    /// Output-only for external inventories; internal generators enqueue via AddLiquid.
    /// </summary>
    public class PumpFluidOutputComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState, IBlockStateObservable
    {
        public string SaveKey  { get; }  = typeof(PumpFluidOutputComponent).FullName;
        public bool CanAcceptGeneratedFluid => _tank.Amount < _tank.Capacity;
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        
        private readonly FluidContainer _tank;
        private readonly BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> _fluidConnector;
        private readonly Subject<Unit> _onChangeBlockState = new();

        public PumpFluidOutputComponent(float capacity, BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> fluidConnector)
        {
            _tank = new FluidContainer(capacity);
            _fluidConnector = fluidConnector;
        }

        public PumpFluidOutputComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> fluidConnector) : this(capacity, fluidConnector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var state) || string.IsNullOrEmpty(state))
            {
                return;
            }

            var json = JsonConvert.DeserializeObject<FluidContainerSaveJsonObject>(state);
            var restoredAmount = Math.Min(json.Amount, _tank.Capacity);

            _tank.Amount = restoredAmount;
            _tank.FluidId = json.FluidId;
        }

        public void Update()
        {
            // Push fluid to connected inventories
            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                if (_tank.Amount <= 0) break;

                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(_tank.Amount, flowRate * GameUpdater.SecondsPerTick);
                if (transferAmount <= 0) continue;

                var stack = new FluidStack(transferAmount, _tank.FluidId);
                var remaining = inventory.AddLiquid(stack, info);
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
            if (_tank.Amount <= 0)
            {
                _tank.FluidId = FluidMaster.EmptyFluidId;
            }

            // 毎tick発火し、同値の握り潰しはChangeBlockStateEventPacketの差分検知に任せる（FluidPipeComponentと同じ約束）
            // Fire every tick and leave identical-payload suppression to ChangeBlockStateEventPacket's diffing, as FluidPipeComponent does
            _onChangeBlockState.OnNext(Unit.Default);

            #region Internal

            double GetFlowRate(ConnectedInfo info)
            {
                if (info.SelfConnector is IFluidConnector fluidConnector)
                {
                    return fluidConnector.Option.FlowCapacity;
                }
                throw new ArgumentException("FluidConnectOption is not set on connector");
            }

            #endregion
        }

        public void EnqueueGeneratedFluid(FluidStack fluidStack)
        {
            _tank.AddLiquid(fluidStack);
        }

        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            // 外部からの注入は拒否する（供給専用）
            // Refuse external injections (supply only)
            return fluidStack;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);

            var state = new FluidContainerSaveJsonObject(_tank);

            return JsonConvert.SerializeObject(state);
        }

        // 内部タンクを出力タンク1本として機械UIと同じ器に載せる
        // Expose the inner tank as a single output tank in the same container the machine UI uses
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var outputTanks = new List<FluidMessagePack> { new(_tank.FluidId, _tank.Amount, _tank.Capacity) };
            var stateDetail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), outputTanks);
            var serialized = MessagePackSerializer.Serialize(stateDetail);

            return new[] { new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, serialized) };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
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
