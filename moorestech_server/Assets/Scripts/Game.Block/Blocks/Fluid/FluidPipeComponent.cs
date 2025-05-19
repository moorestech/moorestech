using System;
using System.Linq;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory
    {
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private BlockPositionInfo _blockPositionInfo;
        
        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
            FluidContainer = new FluidContainer(capacity);
        }
        
        public FluidContainer FluidContainer { get; }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            if (FluidContainer.Amount <= 0) return;
            
            // 流入対象のコンテナを列挙する
            var targetContainers = _connectorComponent.ConnectedTargets
                .Select(kvp => (kvp.Key, kvp.Value, GetMaxFlowRate(kvp.Key.FluidContainer, kvp.Value)))
                .Where(kvp => !FluidContainer.PreviousSourceFluidContainers.Contains(kvp.Key.FluidContainer))
                .Where(kvp => !kvp.Key.FluidContainer.FluidGuid.HasValue || kvp.Key.FluidContainer.FluidGuid == FluidContainer.FluidGuid)
                .OrderBy(kvp => GetMaxFlowRate(kvp.Key.FluidContainer, kvp.Value))
                .ToList();
            
            // 対象のコンテナに向かって流す
            
            for (var i = 0; i < targetContainers.Count; i++)
            {
                var (_, _, maxFlowRate) = targetContainers[i];
                
                var flowPerContainer = FluidContainer.Amount / (targetContainers.Count - i);
                var flowRate = Math.Min(flowPerContainer, maxFlowRate);
                
                if (flowRate <= 0) break;
                
                for (var j = i; j < targetContainers.Count; j++)
                {
                    FluidContainer.Amount -= flowRate;
                    var otherContainer = targetContainers[j].Key.FluidContainer;
                    otherContainer.Amount += flowRate;
                    targetContainers[j] = (targetContainers[j].Key, targetContainers[j].Value, targetContainers[j].Item3 - flowRate);
                    
                    otherContainer.FluidGuid = FluidContainer.FluidGuid;
                    otherContainer.PreviousSourceFluidContainers.Add(FluidContainer);
                }
            }
            
            FluidContainer.PreviousSourceFluidContainers.Clear();
            if (FluidContainer.Amount <= 0) FluidContainer.FluidGuid = null;
            
            // ソートする
            // 最小の流量と渡せる量のどちらか小さい方を対象のすべてに渡す
            // 満たされた対象はリストから削除
            // 元のコンテナから液体がなくなったら終了
            // 全ての対象に繰り返す
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
    }
}