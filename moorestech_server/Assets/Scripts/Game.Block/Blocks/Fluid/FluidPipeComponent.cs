using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory, IBlockStateObservable
    {
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private readonly Subject<Unit> _onChangeBlockState = new();
        private BlockPositionInfo _blockPositionInfo;
        
        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
            FluidContainer = new FluidContainer(capacity);
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
        
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        
        public FluidContainer FluidContainer { get; }
        
        public FluidStack? AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            FluidContainer.AddLiquid(fluidStack, source, out var remain);
            _onChangeBlockState.OnNext(Unit.Default);
            return remain;
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            if (FluidContainer.Amount <= 0) return;
            
            // 流入対象のコンテナを列挙する
            var targetInventories = _connectorComponent.ConnectedTargets
                .Select(kvp => (kvp.Key, kvp.Value, GetMaxFlowRate(kvp.Value)))
                .OrderBy(kvp => kvp.Item3)
                .ToList();
            
            // 対象のコンテナに向かって流す
            
            for (var i = 0; i < targetInventories.Count; i++)
            {
                var (_, _, maxFlowRate) = targetInventories[i];

                var flowPerContainer = FluidContainer.Amount / (targetInventories.Count - i);
                var flowRate = Math.Min(flowPerContainer, maxFlowRate);

                if (flowRate <= 0) break;

                for (var j = i; j < targetInventories.Count; j++)
                {
                    var otherInventory = targetInventories[j].Key;
                    var remain = otherInventory.AddLiquid(new FluidStack(flowRate, FluidContainer.FluidId), FluidContainer);
                    var addedAmount = flowRate - (remain?.Amount ?? 0);
                    FluidContainer.Amount -= addedAmount;
                    targetInventories[j] = (targetInventories[j].Key, targetInventories[j].Value, targetInventories[j].Item3 - addedAmount);
                }
            }
            
            FluidContainer.PreviousSourceFluidContainers.Clear();
            if (FluidContainer.Amount <= 0) FluidContainer.FluidId = FluidMaster.EmptyFluidId;
            
            // ソートする
            // 最小の流量と渡せる量のどちらか小さい方を対象のすべてに渡す
            // 満たされた対象はリストから削除
            // 元のコンテナから液体がなくなったら終了
            // 全ての対象に繰り返す
        }
        
        public FluidPipeStateDetail GetFluidPipeStateDetail()
        {
            var fluidId = FluidContainer.FluidId;
            var amount = FluidContainer.Amount;
            var capacity = FluidContainer.Capacity;
            return new FluidPipeStateDetail(fluidId, (float)amount, (float)capacity);
        }
        
        private double GetMaxFlowRate(ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetOption as FluidConnectOption;

            if (selfOption == null || targetOption == null) throw new ArgumentException();

            return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
        }
    }
}