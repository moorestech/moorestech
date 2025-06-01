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
        private bool _hasReceivedThisUpdate = false;
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            var remain = _fluidContainer.AddLiquid(fluidStack, source);
            if (remain.Amount < fluidStack.Amount) // 何か受け入れた場合
            {
                _hasReceivedThisUpdate = true;
                _onChangeBlockState.OnNext(Unit.Default);
            }
            return remain;
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            // PreviousSourceFluidContainersを常にクリアする必要があるため、
            // 液体がない場合でもUpdate処理を続ける
            var hasFluid = _fluidContainer.Amount > 0;
            
            // Debug: Update開始
            // UnityEngine.Debug.Log($"Update Start - Amount: {_fluidContainer.Amount}, PrevSources: {_fluidContainer.PreviousSourceFluidContainers.Count}");
            
            // Don't send fluid if we received some this update (prevents ping-pong)
            if (hasFluid && !_hasReceivedThisUpdate)
            {
                // 流入対象を列挙する
                var targetInventories = new List<(IFluidInventory inventory, ConnectedInfo info, double maxFlowRate)>();
                foreach (var kvp in _connectorComponent.ConnectedTargets)
                {
                    var targetInventory = kvp.Key;
                    var connectedInfo = kvp.Value;
                    
                    try
                    {
                        // 全てのIFluidInventoryに対して同じ処理
                        var maxFlowRate = GetMaxFlowRateFromConnection(connectedInfo);
                        if (maxFlowRate > 0)
                        {
                            targetInventories.Add((targetInventory, connectedInfo, maxFlowRate));
                        }
                    }
                    catch (Exception e)
                    {
                        // Skip this target if there's an error
                    }
                }
                
                if (targetInventories.Count > 0)
                {
                
                // 流量でソート
                targetInventories = targetInventories.OrderBy(t => t.maxFlowRate).ToList();
                
                // 対象に向かって流す
                // Instead of trying to equalize, just send up to maxFlowRate to each target
                foreach (var target in targetInventories)
                {
                    if (_fluidContainer.Amount <= 0) break;
                    
                    var flowRate = Math.Min(_fluidContainer.Amount, target.maxFlowRate);
                    if (flowRate <= 0) continue;
                    
                    var fluidStack = new FluidStack(flowRate, _fluidContainer.FluidId);
                    var remain = target.inventory.AddLiquid(fluidStack, _fluidContainer);
                    
                    var actualTransferred = flowRate - remain.Amount;
                    _fluidContainer.Amount -= actualTransferred;
                }
                }
            } // hasFluid
            
            // Debug: Update終了前
            // UnityEngine.Debug.Log($"[{_blockPositionInfo.BlockPos}] Before Clear - PrevSources: {_fluidContainer.PreviousSourceFluidContainers.Count}");
            
            _fluidContainer.PreviousSourceFluidContainers.Clear();
            if (_fluidContainer.Amount <= 0) _fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            
            // Reset the flag for next update
            _hasReceivedThisUpdate = false;
            
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
        
        private double GetMaxFlowRateFromConnection(ConnectedInfo connectedInfo)
        {
            var selfOption = connectedInfo.SelfOption as FluidConnectOption;
            var targetOption = connectedInfo.TargetOption as FluidConnectOption;
            
            if (selfOption == null || targetOption == null) throw new ArgumentException();
            
            return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
        }
    }
}