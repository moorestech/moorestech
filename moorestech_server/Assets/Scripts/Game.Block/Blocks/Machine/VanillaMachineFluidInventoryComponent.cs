using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using UniRx;
using Game.Block.Interface.Component.ConnectJudge;

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
        private readonly BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> _fluidConnector;
        private readonly Subject<Unit> _onChangeBlockState = new();

        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public VanillaMachineFluidInventoryComponent(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> fluidConnector)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _fluidConnector = fluidConnector;
        }

        public void Update()
        {
            // 入力: パイプからの転送はAddLiquidメソッドで受動的に処理される
            // Input: transfers from pipes arrive passively through AddLiquid

            // 出力: 機械からパイプへ流体を転送
            // Output: push fluid from the machine into connected pipes
            TransferFromMachineToPipes();

            // 空になったタンクの流体IDをリセットする
            // Reset the fluid id of tanks that have run empty
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.Amount <= 0)
                {
                    container.FluidId = FluidMaster.EmptyFluidId;
                }
            }

            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                if (container.Amount <= 0)
                {
                    container.FluidId = FluidMaster.EmptyFluidId;
                }
            }
        }


        private void TransferFromMachineToPipes()
        {
            // 接続されたパイプに流体を送る
            // Push fluid into connected pipes
            var connectedFluids = _fluidConnector.ConnectedTargets;
            foreach (var (fluidInventory, info) in connectedFluids)
            {
                // SelfConnector（自分側）のConnectTankIndexを取得
                // Get ConnectTankIndex from SelfConnector
                var tankIndex = -1;
                if (info.SelfConnector is IFluidConnector selfConnector)
                {
                    tankIndex = selfConnector.Option.ConnectTankIndex;
                }

                // 対応するタンクが存在しない場合はスキップ
                // Skip when no matching output tank exists
                if (tankIndex < 0 || tankIndex >= _outputInventory.FluidOutputSlot.Count)
                    continue;

                var container = _outputInventory.FluidOutputSlot[tankIndex];
                if (container.Amount <= 0) continue;

                // 流量制限を考慮
                // Respect the flow rate limit
                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(container.Amount, flowRate * GameUpdater.SecondsPerTick);

                var fluidStack = new FluidStack(transferAmount, container.FluidId);
                var remaining = fluidInventory.AddLiquid(fluidStack, info);

                // 転送された量だけコンテナから減らす
                // Subtract only the transferred amount from the container
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

            #region Internal

            double GetFlowRate(ConnectedInfo info)
            {
                if (info.SelfConnector is IFluidConnector fluidConnector)
                {
                    return fluidConnector.Option.FlowCapacity;
                }
                throw new ArgumentException("FluidConnectOption is not set on connector");
            }

            #endregion
        }

        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            // 受け手（自分側）connectorのオプションから流入先タンクを特定する
            // Resolve the destination tank from the receiver-side connector options
            var tankIndex = connectedInfo.TargetConnector is IFluidConnector receiverConnector
                ? receiverConnector.Option.ConnectTankIndex
                : -1;

            // 特定のタンクが指定されている場合は、そのタンクのみに追加を試みる
            // When a specific tank is designated, only that tank accepts the fluid
            if (tankIndex >= 0 && tankIndex < _inputInventory.FluidInputSlot.Count)
            {
                var container = _inputInventory.FluidInputSlot[tankIndex];
                if (container.FluidId == FluidMaster.EmptyFluidId || container.FluidId == fluidStack.FluidId)
                {
                    return container.AddLiquid(fluidStack);
                }
                return fluidStack;
            }

            // タンクが指定されていない場合は、全ての入力タンクに対して液体を追加を試みる
            // Without a designated tank, try every input tank in order
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.FluidId != FluidMaster.EmptyFluidId && container.FluidId != fluidStack.FluidId) continue;

                var remaining = container.AddLiquid(fluidStack);
                if (remaining.Amount < fluidStack.Amount)
                {
                    _onChangeBlockState.OnNext(Unit.Default);
                    return remaining;
                }
            }

            return fluidStack;
        }

        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();

            // 入力タンクの流体を追加
            // Collect fluids from input tanks
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.Amount > 0)
                {
                    fluidStacks.Add(new FluidStack(container.Amount, container.FluidId));
                }
            }

            // 出力タンクの流体を追加
            // Collect fluids from output tanks
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
