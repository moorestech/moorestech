using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using UnityEngine;

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
            FluidContainer = new FluidContainer(capacity, Guid.Empty);
        }
        
        public FluidContainer FluidContainer { get; }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            // TODO: targetFluidContainerが存在するかどうかのチェックを行う
            
            for (var i = FluidContainer.PendingFluidStacks.Count - 1; i >= 0; i--)
            {
                var pendingFluidStack = FluidContainer.PendingFluidStacks[i];
                
                //TODO: キャッシュする
                var targetContainers = new List<FluidContainer>();
                
                // 移動先の候補をリストアップ
                foreach (KeyValuePair<IFluidInventory, ConnectedInfo> kvp in _connectorComponent.ConnectedTargets)
                {
                    // 元のコンテナは除く
                    if (kvp.Key.FluidContainer == pendingFluidStack.PreviousContainer) continue;
                    
                    var selfOption = kvp.Value.SelfOption as FluidConnectOption;
                    var targetOption = kvp.Value.TargetOption as FluidConnectOption;
                    var target = kvp.Value.TargetBlock.GetComponent<IFluidInventory>();
                    if (selfOption == null || targetOption == null || target == null) throw new Exception();
                    
                    // どちらかのflowCapacityが0の場合は除く
                    if (selfOption.FlowCapacity == 0 || targetOption.FlowCapacity == 0) continue;
                    
                    targetContainers.Add(kvp.Key.FluidContainer);
                }
                
                if (targetContainers.Count == 0) continue;
                
                var totalAmount = pendingFluidStack.Amount;
                // 移動先一つに割り当てられるamount
                var amount = totalAmount / targetContainers.Count;
                
                Debug.Log($"amount: {amount}, containers: {targetContainers.Count}");
                // 移動先予定ごとに割り当て
                foreach (var targetContainer in targetContainers)
                {
                    Debug.Log(FluidContainer.FluidStacks.ContainsKey(targetContainer));
                    var newStack = new FluidStack(pendingFluidStack.FluidId, amount, pendingFluidStack.PreviousContainer, targetContainer);
                    FluidContainer.FluidStacks[targetContainer] = newStack;
                }
                
                // 移動先があった場合はpendingListから削除
                FluidContainer.PendingFluidStacks[i] = FluidContainer.PendingFluidStacks[^1];
                FluidContainer.PendingFluidStacks.RemoveAt(FluidContainer.PendingFluidStacks.Count - 1);
            }
            
            // ターゲットに移動する
            foreach (var (targetFluidContainer, fluidStack) in FluidContainer.FluidStacks.Select(kvp => (kvp.Key, kvp.Value)))
            {
                //TODO: 圧倒的に効率が悪いためキャッシュする、もしくはつながりやネットワーク自体をCoreに作る
                var connectInfo = _connectorComponent.ConnectedTargets.First(kvp => kvp.Key.FluidContainer == targetFluidContainer).Value;
                var selfOption = connectInfo.SelfOption as FluidConnectOption;
                var targetOption = connectInfo.TargetOption as FluidConnectOption;
                var target = connectInfo.TargetBlock.GetComponent<IFluidInventory>();
                if (selfOption == null || targetOption == null || target == null) throw new Exception();
                
                var minimumFlowCapacity = Mathf.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * (float)GameUpdater.UpdateSecondTime;
                
                (var transportingStack, FluidStack? remainStack1) = FluidStack.Split(fluidStack, minimumFlowCapacity);
                
                // ターゲットに移動
                targetFluidContainer.AddToPendingList(transportingStack, FluidContainer, out FluidStack? remainStack2);
                
                // 残ったfluidStackは元のコンテナにもどす
                Debug.Log($"{fluidStack.Amount} {remainStack1?.Amount} {minimumFlowCapacity}");
                if (remainStack1.HasValue) FluidContainer.AddToPendingList(remainStack1.Value, fluidStack.PreviousContainer, out _);
                if (remainStack2.HasValue) FluidContainer.AddToPendingList(remainStack2.Value, fluidStack.PreviousContainer, out _);
            }
            // 余ったstackは全てpendingListに戻っているためFluidStacksを削除
            FluidContainer.FluidStacks.Clear();
        }
    }
}