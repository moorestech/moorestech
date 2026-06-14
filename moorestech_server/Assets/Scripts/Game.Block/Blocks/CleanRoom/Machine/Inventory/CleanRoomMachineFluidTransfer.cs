using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;

namespace Game.Block.Blocks.CleanRoom
{
    // 機械タンクとパイプ間の流体転送ロジック（入力受け取り/出力送出）。FluidInventoryComponent から分離した協調オブジェクト。
    // Fluid transfer logic between machine tanks and pipes (input intake / output emit); a collaborator split out of the fluid inventory component.
    public class CleanRoomMachineFluidTransfer
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly CleanRoomMachineOutputInventory _outputInventory;
        private readonly BlockConnectorComponent<IFluidInventory> _fluidConnector;
        private readonly Subject<Unit> _onChangeBlockState;

        public CleanRoomMachineFluidTransfer(
            VanillaMachineInputInventory inputInventory,
            CleanRoomMachineOutputInventory outputInventory,
            BlockConnectorComponent<IFluidInventory> fluidConnector,
            Subject<Unit> onChangeBlockState)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _fluidConnector = fluidConnector;
            _onChangeBlockState = onChangeBlockState;
        }

        public void TransferFromMachineToPipes()
        {
            // 接続されたパイプに流体を送る
            // Send fluid to connected pipes
            var connectedFluids = _fluidConnector.ConnectedTargets;
            foreach (var (fluidInventory, info) in connectedFluids)
            {
                // 自分側コネクタの ConnectTankIndex を取得
                // Get ConnectTankIndex from the self connector
                var tankIndex = -1;
                if (info.SelfConnector?.ConnectOption is FluidConnectOption selfOption)
                {
                    tankIndex = selfOption.ConnectTankIndex;
                }

                if (tankIndex < 0 || tankIndex >= _outputInventory.FluidOutputSlot.Count) continue;

                var container = _outputInventory.FluidOutputSlot[tankIndex];
                if (container.Amount <= 0) continue;

                // 流量制限を考慮して転送
                // Transfer with flow-rate limiting
                var flowRate = GetFlowRate(info);
                var transferAmount = Math.Min(container.Amount, flowRate * GameUpdater.SecondsPerTick);

                var fluidStack = new FluidStack(transferAmount, container.FluidId);
                var remaining = fluidInventory.AddLiquid(fluidStack, container);

                var transferred = transferAmount - remaining.Amount;
                if (transferred > 0)
                {
                    container.Amount -= transferred;
                    _onChangeBlockState.OnNext(Unit.Default);
                }

                if (container.Amount <= 0) container.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        private double GetFlowRate(ConnectedInfo info)
        {
            if (info.SelfConnector?.ConnectOption is FluidConnectOption fluidOption)
            {
                return fluidOption.FlowCapacity;
            }
            throw new ArgumentException("FluidConnectOption is not set on connector");
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // 接続情報から ConnectTankIndex を取得する
            // Resolve the ConnectTankIndex from connection info
            var tankIndex = GetTargetTankIndexFromSource(source);

            // 特定タンクが指定されている場合はそのタンクのみに追加を試みる
            // If a tank is specified, add only to that tank
            if (tankIndex >= 0 && tankIndex < _inputInventory.FluidInputSlot.Count)
            {
                var container = _inputInventory.FluidInputSlot[tankIndex];
                if (container.FluidId == FluidMaster.EmptyFluidId || container.FluidId == fluidStack.FluidId)
                {
                    return container.AddLiquid(fluidStack, source);
                }
                return fluidStack;
            }

            // タンク未指定なら全入力タンクに追加を試みる
            // Otherwise try all input tanks
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
            // 接続先を調べて source がどの接続由来かを特定する
            // Inspect connections to find which one the source came from
            foreach (var (inventory, info) in _fluidConnector.ConnectedTargets)
            {
                if (inventory is FluidPipeComponent pipe)
                {
                    var pipeContainer = GetFluidContainerFromPipe(pipe);
                    if (pipeContainer == source)
                    {
                        if (info.TargetConnector?.ConnectOption is FluidConnectOption targetOption)
                        {
                            return targetOption.ConnectTankIndex;
                        }
                    }
                }
            }

            return -1;
        }

        private FluidContainer GetFluidContainerFromPipe(FluidPipeComponent pipe)
        {
            // リフレクションでプライベートフィールドにアクセス（Vanilla と同じ）
            // Access the private field via reflection (same as Vanilla)
            var field = typeof(FluidPipeComponent).GetField("_fluidContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(pipe) as FluidContainer;
        }
    }
}
