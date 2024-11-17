using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.CraftChainer.BlockComponent;
using Newtonsoft.Json;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChestComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent
    {
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        public VanillaChestComponent(BlockInstanceId blockInstanceId, int slotNum, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            
            _blockInventoryInserter = blockInventoryInserter;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNum);
        }
        
        public VanillaChestComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, int slotNum, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, slotNum, blockInventoryInserter)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(componentStates[SaveKey]);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                var itemStack = itemJsons[i].ToItemStack();
                _itemDataStoreService.SetItem(i, itemStack);
            }
        }
        
        public string SaveKey { get; } = typeof(VanillaChestComponent).FullName;
        public string GetSaveState()
        {
            CheckDestroy(this);
            
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems)
            {
                itemJson.Add(new ItemStackSaveJsonObject(item));
            }
            
            return JsonConvert.SerializeObject(itemJson);
        }
        
        public void Update()
        {
            CheckDestroy(this);
            
            for (var i = 0; i < _itemDataStoreService.InventoryItems.Count; i++)
            {
                var setItem = _blockInventoryInserter.InsertItem(_itemDataStoreService.InventoryItems[i]);
                _itemDataStoreService.SetItem(i, setItem);
            }
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }
        
        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemStack); }
        public IItemStack InsertItem(IItemStack itemStack) { CheckDestroy(this);
            
            var start = TestDebug.TestStartTime;
            var end = System.DateTime.Now;
            var time = end - start;
            var seconds = time.TotalSeconds;
            var pos = ServerContext.WorldBlockDatastore.GetBlock(this).BlockPositionInfo.OriginalPos;
            // DEBUG 消す
            Debug.Log($"chest {itemStack} pos {pos} sec:{seconds:F2}");

            return _itemDataStoreService.InsertItem(itemStack); }
        public int GetSlotSize() { CheckDestroy(this); return _itemDataStoreService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public IItemStack GetItem(int slot) { CheckDestroy(this); return _itemDataStoreService.GetItem(slot); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemId, count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemStacks); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { CheckDestroy(this); return _itemDataStoreService.InsertionCheck(itemStacks); }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}