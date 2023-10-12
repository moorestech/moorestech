using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory : IUpdatable
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly int _entityId;

        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaMachineOutputInventory(int outputSlot, ItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, int entityId, int inputSlotSize)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdater.RegisterUpdater(this);
        }


        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.Inventory;

        public void Update()
        {
            InsertConnectInventory();
        }

        /// <summary>
        ///     アウトプットスロットにアイテムを入れれるかチェック
        /// </summary>
        /// <param name="machineRecipeData"></param>
        /// <returns>スロットに空きがあったらtrue</returns>
        public bool IsAllowedToOutputItem(MachineRecipeData machineRecipeData)
        {
            foreach (var itemOutput in machineRecipeData.ItemOutputs)
            {
                var isAllowed = OutputSlot.Aggregate(false,
                    (current, slot) => slot.IsAllowedToAdd(itemOutput.OutputItem) || current);

                if (!isAllowed) return false;
            }

            return true;
        }

        public void InsertOutputSlot(MachineRecipeData machineRecipeData)
        {
            //アウトプットスロットにアイテムを格納する
            foreach (var output in machineRecipeData.ItemOutputs)
                for (var i = 0; i < OutputSlot.Count; i++)
                {
                    if (!OutputSlot[i].IsAllowedToAdd(output.OutputItem)) continue;

                    var item = OutputSlot[i].AddItem(output.OutputItem).ProcessResultItemStack;
                    _itemDataStoreService.SetItem(i, item);
                    break;
                }
        }

        private void InsertConnectInventory()
        {
            for (var i = 0; i < OutputSlot.Count; i++) _itemDataStoreService.SetItem(i, _connectInventoryService.InsertItem(OutputSlot[i]));
        }

        public void AddConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
            //NullInventoryは削除しておく
            for (var i = _connectInventory.Count - 1; i >= 0; i--)
                if (_connectInventory[i] is NullIBlockInventory)
                    _connectInventory.RemoveAt(i);
        }

        public void RemoveConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory.Remove(blockInventory);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }


        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId, slot + _inputSlotSize, itemStack));
        }
    }
}