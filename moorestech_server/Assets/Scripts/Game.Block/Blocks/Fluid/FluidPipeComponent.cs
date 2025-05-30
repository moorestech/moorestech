using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();
        
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private readonly FluidContainer _fluidContainer;
        private BlockPositionInfo _blockPositionInfo;
        
        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
            _fluidContainer = new FluidContainer(capacity);
        }
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            var fluidStateDetail = GetFluidPipeStateDetail();
            var blockStateDetail = new BlockStateDetail(
                FluidPipeStateDetail.FluidPipeStateDetailKey,
                MessagePackSerializer.Serialize(fluidStateDetail)
            );
            
            return new[] { blockStateDetail };
        }
        
        
        public void AddLiquid(FluidStack fluidStack, FluidContainer source, out FluidStack? remain)
        {
            _fluidContainer.AddLiquid(fluidStack, source, out remain);
            _onChangeBlockState.OnNext(Unit.Default);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            if (_fluidContainer.Amount <= 0) return;
            
            // 流入対象を列挙する
            var targets = _connectorComponent.ConnectedTargets
                .Select(kvp => (kvp.Key, kvp.Value, GetMaxFlowRate(kvp.Value)))
                .ToList();
            
            // 対象に向かって流す
            foreach (var (targetInventory, connectedInfo, maxFlowRate) in targets)
            {
                if (_fluidContainer.Amount <= 0) break;
                
                var flowRate = Math.Min(_fluidContainer.Amount, maxFlowRate);
                if (flowRate <= 0) continue;
                
                var fluidToTransfer = new FluidStack(flowRate, _fluidContainer.FluidId);
                targetInventory.AddLiquid(fluidToTransfer, _fluidContainer, out var remain);
                
                var actualTransferred = flowRate - (remain?.Amount ?? 0);
                _fluidContainer.Amount -= actualTransferred;
            }
            
            _fluidContainer.PreviousSourceFluidContainers.Clear();
            if (_fluidContainer.Amount <= 0) _fluidContainer.FluidId = FluidMaster.EmptyFluidId;
        }
        
        public FluidPipeStateDetail GetFluidPipeStateDetail()
        {
            var fluidId = _fluidContainer.FluidId;
            var amount = _fluidContainer.Amount;
            var capacity = _fluidContainer.Capacity;
            return new FluidPipeStateDetail(fluidId, (float)amount, (float)capacity);
        }
        
        private double GetMaxFlowRate(ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetOption as FluidConnectOption;
            
            if (selfOption == null || targetOption == null) throw new ArgumentException();
            
            var flowRate = Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
            
            return flowRate;
        }
    }
}