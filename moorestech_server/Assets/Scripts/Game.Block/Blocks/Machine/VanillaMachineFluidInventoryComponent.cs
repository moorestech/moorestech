using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    /// 機械用の流体インベントリコンポーネント
    /// パイプとの接続をサポートし、流体の入出力を管理する
    /// </summary>
    public class VanillaMachineFluidInventoryComponent : IFluidInventory, IUpdatableBlockComponent
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        // タンク数を管理
        private readonly int _tankCount;
        
        public VanillaMachineFluidInventoryComponent(
            VanillaMachineInputInventory inputInventory,
            BlockConnectorComponent<IFluidInventory> fluidConnector,
            int tankCount)
        {
            _inputInventory = inputInventory;
            _fluidConnector = fluidConnector;
            _tankCount = tankCount;
            
            // _connectInventoryService = new ConnectingInventoryListPriorityGetItemService<IFluidInventory>(_fluidConnector);
        }
        
        public void Update()
        {
            // 入力: パイプからの転送はAddLiquidメソッドで受動的に処理される
            
            // 出力: 機械からパイプへ流体を転送
            TransferFromMachineToPipes();
            
            // 各タンクのPreviousSourceFluidContainersをクリア
            for (var i = 0; i < _tankCount; i++)
            {
                var previousCount = _inputInventory.FluidInputSlot[i].PreviousSourceFluidContainers.Count;
                _inputInventory.FluidInputSlot[i].PreviousSourceFluidContainers.Clear();
                
                // タンクが空の場合はFluidIdをリセット
                if (_inputInventory.FluidInputSlot[i].Amount <= 0)
                {
                    _inputInventory.FluidInputSlot[i].FluidId = FluidMaster.EmptyFluidId;
                }
            }
        }
        
        
        private void TransferFromMachineToPipes()
        {
            // 全てのタンクから出力を試みる
            for (var i = 0; i < _tankCount; i++)
            {
                var container = _inputInventory.FluidInputSlot[i];
                if (container.Amount <= 0) continue;
                
                // 接続されたパイプに流体を送る
                var connectedFluids = _fluidConnector.ConnectedTargets;
                foreach (var (fluidInventory, info) in connectedFluids)
                {
                    if (container.Amount <= 0) break;
                    
                    // 流量制限を考慮
                    var flowRate = GetFlowRate(info);
                    var transferAmount = System.Math.Min(container.Amount, flowRate * 1.0); // TODO: 適切なDeltaTime実装
                    
                    var fluidStack = new FluidStack(transferAmount, container.FluidId);
                    var remaining = fluidInventory.AddLiquid(fluidStack, container);
                    
                    // 転送された量だけコンテナから減らす
                    var transferred = transferAmount - remaining.Amount;
                    container.Amount -= transferred;
                    
                    if (container.Amount <= 0)
                    {
                        container.FluidId = FluidMaster.EmptyFluidId;
                    }
                }
            }
        }
        
        private double GetFlowRate(ConnectedInfo info)
        {
            // 接続情報から流量を取得
            if (info.SelfOption is FluidConnectOption fluidOption)
            {
                return fluidOption.FlowCapacity;
            }
            return 10.0; // デフォルト流量
        }
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // 全てのタンクに対して液体を追加を試みる
            for (var i = 0; i < _tankCount; i++)
            {
                var container = _inputInventory.FluidInputSlot[i];
                if (container.FluidId != FluidMaster.EmptyFluidId && container.FluidId != fluidStack.FluidId) continue;
                
                var remaining = container.AddLiquid(fluidStack, source);
                if (remaining.Amount < fluidStack.Amount)
                {
                    return remaining;
                }
            }
            
            return fluidStack;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}