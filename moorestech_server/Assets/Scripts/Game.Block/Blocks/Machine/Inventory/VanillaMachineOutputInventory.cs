using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Fluid;
using UniRx;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory : IVanillaMachineSubInventory
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
        
        public VanillaMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> blockConnectorComponent)
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
        }

        /// <summary>
        ///     産出スタック列を格納できるか仮想挿入で判定する
        ///     Check via virtual insertion whether the output stacks fit
        /// </summary>
        public bool CanStoreOutputs(IReadOnlyList<IItemStack> itemOutputs, IReadOnlyList<FluidStack> fluidOutputs)
        {
            // 液体出力のスペースを先に確認する
            // Check fluid output space first
            if (!IsFluidOutputAllowed()) return false;

            // スロット複製へ仮想挿入して空きを判定（実挿入と同順）
            // Virtually insert into copied slots (same order as the real insert)
            var simulatedSlots = OutputSlot.ToList();
            foreach (var outputItemStack in itemOutputs)
            {
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

            #region Internal

            bool IsFluidOutputAllowed()
            {
                // 液体の出力スペースをチェック
                // Check output space for fluids
                for (var i = 0; i < fluidOutputs.Count; i++)
                {
                    if (i >= _fluidContainers.Length) return false;

                    var outputFluid = fluidOutputs[i];

                    // 既に異なる液体が入っている場合、または容量が不足している場合
                    // If a different fluid is already present, or the remaining capacity is insufficient
                    if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != outputFluid.FluidId)
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

            #endregion
        }

        /// <summary>
        ///     アイテム出力と液体出力を格納する
        ///     Insert the item and fluid outputs
        /// </summary>
        public void InsertOutputSlot(IReadOnlyList<IItemStack> itemOutputs, IReadOnlyList<FluidStack> fluidOutputs)
        {
            //アウトプットスロットにアイテムを格納する
            InsertItemOutputs();

            //アウトプットスロットに液体を格納する
            for (var i = 0; i < fluidOutputs.Count; i++)
            {
                if (i >= _fluidContainers.Length) break;

                _fluidContainers[i].AddLiquid(fluidOutputs[i]);
            }

            #region Internal

            void InsertItemOutputs()
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

            #endregion
        }
        
        // 産出スロットを接続先インベントリへ払い出す。駆動はプロセッサコンポーネントのUpdate（自前購読は持たない）
        // Push output slots into connected inventories; driven by the processor component's Update (no self subscription)
        public void InsertConnectInventory()
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