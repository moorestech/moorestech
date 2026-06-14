using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineFluidInventoryComponent をコピーし、出力側を CleanRoomMachineOutputInventory に差し替えた専用版。
    // 入出力の転送ロジックは CleanRoomMachineFluidTransfer へ委譲し、本体はタンク掃除と状態表示に専念する。
    // Copied from VanillaMachineFluidInventoryComponent; output swapped to CleanRoomMachineOutputInventory.
    // Transfer logic is delegated to CleanRoomMachineFluidTransfer; this component handles tank cleanup and state reporting.
    public class CleanRoomMachineFluidInventoryComponent : IFluidInventory, IUpdatableBlockComponent, IBlockStateObservable
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly CleanRoomMachineOutputInventory _outputInventory;
        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly CleanRoomMachineFluidTransfer _transfer;

        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public CleanRoomMachineFluidInventoryComponent(
            VanillaMachineInputInventory inputInventory,
            CleanRoomMachineOutputInventory outputInventory,
            BlockConnectorComponent<IFluidInventory> fluidConnector)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _transfer = new CleanRoomMachineFluidTransfer(inputInventory, outputInventory, fluidConnector, _onChangeBlockState);
        }

        public void Update()
        {
            // 入力: パイプからの転送は AddLiquid で受動的に処理される
            // Input: pipe transfers are handled passively via AddLiquid

            // 出力: 機械からパイプへ流体を転送
            // Output: transfer fluid from the machine to pipes
            _transfer.TransferFromMachineToPipes();

            // 入力タンクの送信元記録をクリア
            // Clear input-tank source records
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                container.ClearPreviousSources();
                if (container.Amount <= 0) container.FluidId = FluidMaster.EmptyFluidId;
            }

            // 出力タンクの送信元記録をクリア
            // Clear output-tank source records
            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                container.ClearPreviousSources();
                if (container.Amount <= 0) container.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        // 入力: パイプ側から呼ばれる受動的な液体受け取り（転送オブジェクトへ委譲）。
        // Input: passive liquid intake invoked by pipes (delegated to the transfer object).
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            return _transfer.AddLiquid(fluidStack, source);
        }

        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();

            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.Amount > 0) fluidStacks.Add(new FluidStack(container.Amount, container.FluidId));
            }

            foreach (var container in _outputInventory.FluidOutputSlot)
            {
                if (container.Amount > 0) fluidStacks.Add(new FluidStack(container.Amount, container.FluidId));
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
