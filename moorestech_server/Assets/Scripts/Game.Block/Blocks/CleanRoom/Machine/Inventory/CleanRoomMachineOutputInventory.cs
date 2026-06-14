using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Blocks.Machine.Inventory;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineOutputInventory をコピーして専用化。出力抽選ロジックは CleanRoomMachineOutputEmitter へ委譲し、本体はスロット保持と搬出を担う。
    // Copied from VanillaMachineOutputInventory; the output draw logic is delegated to CleanRoomMachineOutputEmitter while this holds slots and pushes items out.
    public class CleanRoomMachineOutputInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => OutputSlot;
        public IReadOnlyList<FluidContainer> FluidOutputSlot => _fluidContainers;

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;

        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;
        private readonly CleanRoomMachineOutputEmitter _emitter;

        public CleanRoomMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory> blockConnectorComponent,
            CleanRoomStateReceiverComponent receiver, Action onNoOutputForTest)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, blockConnectorComponent);

            _fluidContainers = new FluidContainer[outputTankCount];
            for (var i = 0; i < outputTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }

            _emitter = new CleanRoomMachineOutputEmitter(_itemDataStoreService, _fluidContainers, receiver, onNoOutputForTest);

            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        // 出力可否判定（レベル付き出力の空スロット予約含む）は emitter へ委譲する。
        // Output-permission check (incl. empty-slot reservation for leveled outputs) is delegated to the emitter.
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            return _emitter.IsAllowedToOutputItem(machineRecipe);
        }

        // サイクル完了時の出力確定（決定的抽選）は emitter へ委譲する。
        // Cycle-complete output emission (deterministic draw) is delegated to the emitter.
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe, long cycleSeed)
        {
            _emitter.InsertOutputSlot(machineRecipe, cycleSeed);
        }

        private void Update()
        {
            InsertConnectInventory();
        }

        private void InsertConnectInventory()
        {
            for (var i = 0; i < OutputSlot.Count; i++)
                _itemDataStoreService.SetItem(i, _connectInventoryService.InsertItem(OutputSlot[i]));
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot + _inputSlotSize, itemStack));
        }
    }
}
