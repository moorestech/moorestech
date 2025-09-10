using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    ///     GearPumpから流体を周囲のパイプに送り出すコンポーネント
    /// </summary>
    public class GearPumpFluidOutputComponent : IUpdatableBlockComponent
    {
        private readonly FluidContainer _fluidContainer;
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        
        public GearPumpFluidOutputComponent(
            FluidContainer fluidContainer,
            BlockConnectorComponent<IFluidInventory> connectorComponent)
        {
            _fluidContainer = fluidContainer;
            _connectorComponent = connectorComponent;
        }
        
        public void Update()
        {
            if (_fluidContainer.Amount <= 0) return;
            
            // 流出対象を列挙
            var targetInventories = GetTargetInventories();
            if (targetInventories.Count <= 0) return;
            
            // 流量でソート
            targetInventories = targetInventories.OrderBy(t => t.maxFlowRate).ToList();
            
            // 対象に向かって流す
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
            
            // コンテナが空になったらFluidIdをリセット
            if (_fluidContainer.Amount <= 0)
            {
                _fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            }
            
            #region Internal
            
            List<(IFluidInventory inventory, ConnectedInfo info, double maxFlowRate)> GetTargetInventories()
            {
                var result = new List<(IFluidInventory inventory, ConnectedInfo info, double maxFlowRate)>();
                foreach (var kvp in _connectorComponent.ConnectedTargets)
                {
                    var targetInventory = kvp.Key;
                    var connectedInfo = kvp.Value;
                    
                    var maxFlowRate = GetMaxFlowRateFromConnection(connectedInfo);
                    if (maxFlowRate > 0)
                    {
                        result.Add((targetInventory, connectedInfo, maxFlowRate));
                    }
                }
                
                return result;
            }
            
            double GetMaxFlowRateFromConnection(ConnectedInfo connectedInfo)
            {
                var selfOption = connectedInfo.SelfOption as FluidConnectOption;
                var targetOption = connectedInfo.TargetOption as FluidConnectOption;
                
                if (selfOption == null || targetOption == null) return 0;
                
                return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.UpdateSecondTime;
            }
            
            #endregion
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}