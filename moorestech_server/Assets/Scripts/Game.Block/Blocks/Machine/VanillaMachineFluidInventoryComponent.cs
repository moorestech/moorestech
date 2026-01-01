using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    /// 機械用の流体インベントリコンポーネント
    /// パイプとの接続をサポートし、流体の入出力を管理する
    /// </summary>
    public class VanillaMachineFluidInventoryComponent : IFluidInventory, IUpdatableBlockComponent, IBlockStateObservable
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly VanillaMachineOutputInventory _outputInventory;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        private readonly Subject<Unit> _onChangeBlockState = new();
        
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        
        public VanillaMachineFluidInventoryComponent(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _fluidConnector = fluidConnector;
        }
        
        public void Update()
        {
            // 入力: パイプからの転送はAddLiquidメソッドで受動的に処理される
            
            // 出力: 機械からパイプへ流体を転送
            TransferFromMachineToPipes();
            
            // 入力タンクのPreviousSourceFluidContainersをクリア
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                container.PreviousSourceFluidContainers.Clear();
                
                // タンクが空の場合はFluidIdをリセット
                if (container.Amount <= 0)
                {
                    container.FluidId = FluidMaster.EmptyFluidId;
                }
            }
            
            // 出力タンクのPreviousSourceFluidContainersをクリア
            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                container.PreviousSourceFluidContainers.Clear();
                
                // タンクが空の場合はFluidIdをリセット
                if (container.Amount <= 0)
                {
                    container.FluidId = FluidMaster.EmptyFluidId;
                }
            }
        }
        
        
        private void TransferFromMachineToPipes()
        {
            // 接続されたパイプに流体を送る
            var connectedFluids = _fluidConnector.ConnectedTargets;
            foreach (var (fluidInventory, info) in connectedFluids)
            {
                // SelfConnector（自分側）のConnectTankIndexを取得
                // Get ConnectTankIndex from SelfConnector
                var selfOption = BlockConnectorOptionReader.ReadFluidOption(info.SelfConnector?.Option);
                var tankIndex = selfOption.HasValue ? selfOption.Value.ConnectTankIndex : -1;
                
                // 対応するタンクが存在しない場合はスキップ
                if (tankIndex < 0 || tankIndex >= _outputInventory.FluidOutputSlot.Count)
                    continue;
                
                var container = _outputInventory.FluidOutputSlot[tankIndex];
                if (container.Amount <= 0) continue;
                
                // 流量制限を考慮
                var flowRate = GetFlowRate(info);
                var transferAmount = System.Math.Min(container.Amount, flowRate * 1.0); // TODO: 適切なDeltaTime実装
                
                var fluidStack = new FluidStack(transferAmount, container.FluidId);
                var remaining = fluidInventory.AddLiquid(fluidStack, container);
                
                // 転送された量だけコンテナから減らす
                var transferred = transferAmount - remaining.Amount;
                if (transferred > 0)
                {
                    container.Amount -= transferred;
                    _onChangeBlockState.OnNext(Unit.Default);
                }
                
                if (container.Amount <= 0)
                {
                    container.FluidId = FluidMaster.EmptyFluidId;
                }
            }
        }
        
        private double GetFlowRate(ConnectedInfo info)
        {
            // 接続情報から流量を取得
            // Get flow rate from connection info
            var fluidOption = BlockConnectorOptionReader.ReadFluidOption(info.SelfConnector?.Option);
            return fluidOption.HasValue ? fluidOption.Value.FlowCapacity : 10.0;
        }
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // 接続情報からConnectTankIndexを取得する
            var tankIndex = GetTargetTankIndexFromSource(source);
            
            // 特定のタンクが指定されている場合は、そのタンクのみに追加を試みる
            if (tankIndex >= 0 && tankIndex < _inputInventory.FluidInputSlot.Count)
            {
                var container = _inputInventory.FluidInputSlot[tankIndex];
                if (container.FluidId == FluidMaster.EmptyFluidId || container.FluidId == fluidStack.FluidId)
                {
                    return container.AddLiquid(fluidStack, source);
                }
                return fluidStack;
            }
            
            // タンクが指定されていない場合は、全ての入力タンクに対して液体を追加を試みる
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.FluidId != FluidMaster.EmptyFluidId && container.FluidId != fluidStack.FluidId) continue;
                
                var remaining = container.AddLiquid(fluidStack, source);
                if (remaining.Amount < fluidStack.Amount)
                {
                    _onChangeBlockState.OnNext(Unit.Default);
                    return remaining;
                }
            }
            
            return fluidStack;
        }
        
        private int GetTargetTankIndexFromSource(FluidContainer source)
        {
            // 接続されているコンポーネントを調べて、sourceがどの接続から来たかを特定する
            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                // FluidPipeComponentの場合、そのコンテナと比較
                if (inventory is FluidPipeComponent pipe)
                {
                    var pipeContainer = GetFluidContainerFromPipe(pipe);
                    if (pipeContainer == source)
                    {
                        // ターゲット側（自分側）のConnectOptionからConnectTankIndexを取得
                        // Get ConnectTankIndex from TargetConnector
                        var targetOption = BlockConnectorOptionReader.ReadFluidOption(info.TargetConnector?.Option);
                        if (targetOption.HasValue) return targetOption.Value.ConnectTankIndex;
                    }
                }
            }
            
            return -1; // タンクが特定できない場合
        }
        
        private FluidContainer GetFluidContainerFromPipe(FluidPipeComponent pipe)
        {
            // リフレクションを使用してプライベートフィールドにアクセス
            var field = typeof(FluidPipeComponent).GetField("_fluidContainer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(pipe) as FluidContainer;
        }
        
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            
            // 入力タンクの流体を追加
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.Amount > 0)
                {
                    fluidStacks.Add(new FluidStack(container.Amount, container.FluidId));
                }
            }
            
            // 出力タンクの流体を追加
            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                if (container.Amount > 0)
                {
                    fluidStacks.Add(new FluidStack(container.Amount, container.FluidId));
                }
            }
            
            return fluidStacks;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState?.Dispose();
        }
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            var inputTanks = new List<FluidMessagePack>();
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                inputTanks.Add(new FluidMessagePack(container.FluidId, container.Amount, container.Capacity));
            }
            
            var outputTanks = new List<FluidMessagePack>();
            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                outputTanks.Add(new FluidMessagePack(container.FluidId, container.Amount, container.Capacity));
            }
            
            var stateDetail = new FluidMachineInventoryStateDetail(inputTanks, outputTanks);
            var serialized = MessagePackSerializer.Serialize(stateDetail);
            
            return new[] { new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, serialized) };
        }
    }
}
