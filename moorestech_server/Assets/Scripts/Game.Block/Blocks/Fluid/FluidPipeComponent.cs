using System;
using System.Collections.Generic;
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
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private readonly Subject<Unit> _onChangeBlockState = new();
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
        
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        
        // Private field - tests access via reflection
        private readonly FluidContainer _fluidContainer;
        
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
            var targetInventories = new List<(IFluidInventory inventory, ConnectedInfo info, double maxFlowRate)>();
            
            foreach (var kvp in _connectorComponent.ConnectedTargets)
            {
                var targetInventory = kvp.Key;
                var connectedInfo = kvp.Value;
                
                // FluidPipeComponentの場合のみ、PreviousSourceFluidContainersのチェックを行う
                if (targetInventory is FluidPipeComponent targetPipe)
                {
                    // 自分が前回のソースだった場合はスキップ
                    if (_fluidContainer.PreviousSourceFluidContainers.Contains(targetPipe._fluidContainer))
                    {
                        continue;
                    }
                    
                    // 異なる液体が入っている場合はスキップ
                    if (targetPipe._fluidContainer.FluidId != FluidMaster.EmptyFluidId && 
                        targetPipe._fluidContainer.FluidId != _fluidContainer.FluidId)
                    {
                        continue;
                    }
                    
                    var maxFlowRate = GetMaxFlowRate(targetPipe._fluidContainer, connectedInfo);
                    if (maxFlowRate > 0)
                    {
                        targetInventories.Add((targetInventory, connectedInfo, maxFlowRate));
                    }
                }
                else
                {
                    // FluidPipeComponent以外のIFluidInventoryの場合
                    // 最大流量は接続情報のみから計算
                    var maxFlowRate = GetMaxFlowRateFromConnection(connectedInfo);
                    if (maxFlowRate > 0)
                    {
                        targetInventories.Add((targetInventory, connectedInfo, maxFlowRate));
                    }
                }
            }
            
            if (targetInventories.Count == 0) return;
            
            // 流量でソート
            targetInventories = targetInventories.OrderBy(t => t.maxFlowRate).ToList();
            
            // 対象に向かって流す
            for (var i = 0; i < targetInventories.Count; i++)
            {
                var maxFlowRate = targetInventories[i].maxFlowRate;
                
                var flowPerContainer = _fluidContainer.Amount / (targetInventories.Count - i);
                var flowRate = Math.Min(flowPerContainer, maxFlowRate);
                
                if (flowRate <= 0) break;
                
                for (var j = i; j < targetInventories.Count; j++)
                {
                    var targetInventory = targetInventories[j].inventory;
                    var fluidStack = new FluidStack(flowRate, _fluidContainer.FluidId);
                    targetInventory.AddLiquid(fluidStack, _fluidContainer, out var remain);
                    
                    var actualTransferred = flowRate - (remain?.Amount ?? 0);
                    _fluidContainer.Amount -= actualTransferred;
                    
                    targetInventories[j] = (targetInventories[j].inventory, targetInventories[j].info, targetInventories[j].maxFlowRate - actualTransferred);
                }
            }
            
            _fluidContainer.PreviousSourceFluidContainers.Clear();
            if (_fluidContainer.Amount <= 0) _fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            
            // ソートする
            // 最小の流量と渡せる量のどちらか小さい方を対象のすべてに渡す
            // 満たされた対象はリストから削除
            // 元のコンテナから液体がなくなったら終了
            // 全ての対象に繰り返す
        }
        
        public FluidPipeStateDetail GetFluidPipeStateDetail()
        {
            var fluidId = _fluidContainer.FluidId;
            var amount = _fluidContainer.Amount;
            var capacity = _fluidContainer.Capacity;
            return new FluidPipeStateDetail(fluidId, (float)amount, (float)capacity);
        }
        
        private double GetMaxFlowRate(FluidContainer container, ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetOption as FluidConnectOption;
            
            if (selfOption == null || targetOption == null) throw new ArgumentException();
            
            var flowRate = Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
            flowRate = Math.Min(flowRate, container.Capacity - container.Amount);
            
            return flowRate;
        }
        
        private double GetMaxFlowRateFromConnection(ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetOption as FluidConnectOption;
            
            if (selfOption == null || targetOption == null) throw new ArgumentException();
            
            return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
        }
    }
}