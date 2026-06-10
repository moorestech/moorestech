using System;
using System.Collections.Generic;
using System.Linq;
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
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        public IReadOnlyList<FluidContainer> FluidOutputSlot => _fluidContainers;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;
        
        public VanillaMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
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
            
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        private void Update()
        {
            InsertConnectInventory();
        }
        
        /// <summary>
        ///     ベース1セット＋追加extraSetsセットのアイテム出力を格納できるか仮想挿入で判定する。液体はベース1セット分のみチェック
        ///     Check via virtual insertion whether one base set plus extraSets item output sets fit. Fluids are checked for the base set only
        /// </summary>
        public bool CanStoreOutputs(MachineRecipeMasterElement machineRecipe, int extraSets, Func<IItemStack, IItemStack> transformOutputItem)
        {
            // 液体出力のスペースを先に確認する
            // Check fluid output space first
            if (!IsFluidOutputAllowed(machineRecipe)) return false;

            // 現在のスロットを複製し、全セットを順番に仮想挿入して空きを判定する
            // Copy the current slots and virtually insert every set sequentially to test capacity
            var simulatedSlots = OutputSlot.ToList();
            var totalSets = 1 + extraSets;
            for (var set = 0; set < totalSets; set++)
            {
                if (!TryVirtualInsert()) return false;
            }

            return true;

            #region Internal

            bool TryVirtualInsert()
            {
                foreach (var itemOutput in machineRecipe.OutputItems)
                {
                    var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);

                    // 品質変種など実出力と同じ変換を仮想挿入にも適用し、予約と実挿入のスタック分離を一致させる
                    // Apply the same transform as the real output (e.g. quality variants) so reservation matches actual stack separation
                    var outputItemStack = transformOutputItem(ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count));

                    var inserted = false;
                    for (var i = 0; i < simulatedSlots.Count; i++)
                    {
                        if (!simulatedSlots[i].IsAllowedToAdd(outputItemStack)) continue;

                        simulatedSlots[i] = simulatedSlots[i].AddItem(outputItemStack).ProcessResultItemStack;
                        inserted = true;
                        break;
                    }

                    if (!inserted) return false;
                }

                return true;
            }

            #endregion
        }

        private bool IsFluidOutputAllowed(MachineRecipeMasterElement machineRecipe)
        {
            // 液体の出力スペースをチェック
            // Check output space for fluids
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) return false;

                var outputFluid = machineRecipe.OutputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);

                // 既に異なる液体が入っている場合、または容量が不足している場合
                // If a different fluid is already present, or the remaining capacity is insufficient
                if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != fluidId)
                {
                    return false;
                }

                if (_fluidContainers[i].Capacity - _fluidContainers[i].Amount < outputFluid.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     変換済みアイテム出力1セットと、レシピ定義の液体出力を格納する
        ///     Insert one pre-transformed set of item outputs plus the recipe-defined fluid outputs
        /// </summary>
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe, IReadOnlyList<IItemStack> itemOutputs)
        {
            //アウトプットスロットにアイテムを格納する
            InsertItemOutputsOnly(itemOutputs);

            //アウトプットスロットに液体を格納する
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) break;

                var outputFluid = machineRecipe.OutputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                var fluidStack = new FluidStack(outputFluid.Amount, fluidId);

                _fluidContainers[i].AddLiquid(fluidStack, FluidContainer.Empty);
            }
        }

        /// <summary>
        ///     変換済みアイテム出力1セット分のみを格納する（液体は格納しない）。生産性モジュールの追加出力用
        ///     Insert exactly one pre-transformed set of item outputs (no fluids). Used for productivity module extra output
        /// </summary>
        public void InsertItemOutputsOnly(IReadOnlyList<IItemStack> itemOutputs)
        {
            foreach (var outputItemStack in itemOutputs)
                for (var i = 0; i < OutputSlot.Count; i++)
                {
                    if (!OutputSlot[i].IsAllowedToAdd(outputItemStack)) continue;

                    var item = OutputSlot[i].AddItem(outputItemStack).ProcessResultItemStack;
                    _itemDataStoreService.SetItem(i, item);
                    break;
                }
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