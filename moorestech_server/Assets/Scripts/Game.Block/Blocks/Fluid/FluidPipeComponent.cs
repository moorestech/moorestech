using System;
using System.Collections.Generic;
using System.Linq;
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
            foreach (KeyValuePair<IFluidInventory, ConnectedInfo> kvp in _connectorComponent.ConnectedTargets)
            {
                var selfOption = kvp.Value.SelfOption as FluidConnectOption;
                var targetOption = kvp.Value.TargetOption as FluidConnectOption;
                var target = kvp.Value.TargetBlock.GetComponent<IFluidInventory>();
                var fluidInventory = kvp.Key;
                
                if (selfOption == null || targetOption == null || target == null) throw new Exception();
            }
            
            // TODO: targetFluidContainerが存在するかどうかのチェックを行う
            
            for (var i = FluidContainer.PendingFluidStacks.Count - 1; i >= 0; i--)
            {
                var pendingFluidStack = FluidContainer.PendingFluidStacks[i];
                
                //TODO: キャッシュする
                var targetContainers = new List<FluidContainer>();
                foreach (KeyValuePair<IFluidInventory, ConnectedInfo> kvp in _connectorComponent.ConnectedTargets)
                {
                    // 元のコンテナは除く
                    if (kvp.Key.FluidContainer == pendingFluidStack.PreviousContainer) continue;
                    targetContainers.Add(kvp.Key.FluidContainer);
                }
                
                var totalAmount = pendingFluidStack.Amount;
                // 一つのamount
                var amount = totalAmount / targetContainers.Count;
                
                foreach (var targetContainer in targetContainers)
                {
                    var newStack = new FluidStack(pendingFluidStack.FluidId, amount, pendingFluidStack.PreviousContainer, targetContainer);
                    FluidContainer.FluidStacks[targetContainer] = newStack;
                }
                
                // 移動先した場合はpendingListから削除
                if (targetContainers.Count != 0)
                {
                    FluidContainer.PendingFluidStacks[i] = FluidContainer.PendingFluidStacks[^1];
                    FluidContainer.PendingFluidStacks.RemoveAt(FluidContainer.PendingFluidStacks.Count - 1);
                }
            }
            
            // ターゲットに移動する
            foreach (var (targetFluidContainer, fluidStack) in FluidContainer.FluidStacks.Select(kvp => (kvp.Key, kvp.Value)))
            {
                // ターゲットに移動
                targetFluidContainer.AddToPendingList(fluidStack, FluidContainer, out FluidStack? remainFluidStack);
                Debug.Log(targetFluidContainer.TotalAmount);
                
                // 残ったfluidStackは元のコンテナにもどす
                if (remainFluidStack.HasValue)
                {
                    FluidContainer.AddToPendingList(remainFluidStack.Value, fluidStack.PreviousContainer, out _);
                }
            }
            // 余ったstackは全てpendingListに戻っているためFluidStacksを削除
            FluidContainer.FluidStacks.Clear();
        }
    }
}