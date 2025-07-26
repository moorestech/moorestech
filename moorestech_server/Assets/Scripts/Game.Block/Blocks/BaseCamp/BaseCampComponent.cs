using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.BaseCamp
{
    /// <summary>
    /// ベースキャンプブロックのコンポーネント
    /// Base camp block component
    /// </summary>
    public class BaseCampComponent : IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public BlockInstanceId BlockInstanceId { get; }
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly Dictionary<ItemId, int> _requiredItems;
        
        public BaseCampComponent(BlockInstanceId blockInstanceId, BaseCampBlockParam blockParam)
        {
            BlockInstanceId = blockInstanceId;
            
            // 必要アイテムをDictionaryに変換
            // Convert required items to Dictionary
            _requiredItems = new Dictionary<ItemId, int>();
            foreach (var item in blockParam.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(item.ItemGuid);
                _requiredItems[itemId] = item.Amount;
            }
            
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, blockParam.InventorySlot);
        }
        
        public BaseCampComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, 
            BaseCampBlockParam blockParam) : this(blockInstanceId, blockParam)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(componentStates[SaveKey]);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                var itemStack = itemJsons[i].ToItemStack();
                _itemDataStoreService.SetItem(i, itemStack);
            }
        }
        
        /// <summary>
        /// 必要なアイテムがすべて納品されたかどうかを確認する
        /// Check if all required items have been delivered
        /// </summary>
        public bool IsCompleted()
        {
            CheckDestroy(this);
            
            // 現在のインベントリ内のアイテムを集計
            // Count items currently in inventory
            var currentItems = new Dictionary<ItemId, int>();
            foreach (var item in InventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                
                if (!currentItems.ContainsKey(item.Id))
                    currentItems[item.Id] = 0;
                currentItems[item.Id] += item.Count;
            }
            
            // 必要なアイテムがすべて揃っているか確認
            // Check if all required items are present
            foreach (var required in _requiredItems)
            {
                if (!currentItems.ContainsKey(required.Key) || currentItems[required.Key] < required.Value)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 納品の進捗率を取得する（0.0～1.0）
        /// Get delivery progress (0.0 to 1.0)
        /// </summary>
        public float GetProgress()
        {
            CheckDestroy(this);
            
            // 現在のインベントリ内のアイテムを集計
            // Count items currently in inventory
            var currentItems = new Dictionary<ItemId, int>();
            foreach (var item in InventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                
                if (!currentItems.ContainsKey(item.Id))
                    currentItems[item.Id] = 0;
                currentItems[item.Id] += item.Count;
            }
            
            // 必要な総アイテム数と納品済みアイテム数を計算
            // Calculate total required items and delivered items
            var totalRequired = _requiredItems.Sum(r => r.Value);
            var totalDelivered = 0;
            
            foreach (var required in _requiredItems)
            {
                if (currentItems.ContainsKey(required.Key))
                {
                    totalDelivered += System.Math.Min(currentItems[required.Key], required.Value);
                }
            }
            
            return totalRequired > 0 ? (float)totalDelivered / totalRequired : 0f;
        }
        
        #region IOpenableBlockInventoryComponent
        
        public IItemStack GetItem(int slot)
        {
            CheckDestroy(this);
            return _itemDataStoreService.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        public void SetItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            _itemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            
            // 必要なアイテムのみ受け入れる
            // Accept only required items
            if (!_requiredItems.ContainsKey(itemStack.Id))
            {
                return itemStack; // 受け入れないアイテムはそのまま返す / Return items that are not accepted
            }
            
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public IItemStack InsertItem(ItemId itemId, int count)
        {
            CheckDestroy(this);
            
            // 必要なアイテムのみ受け入れる
            // Accept only required items
            if (!_requiredItems.ContainsKey(itemId))
            {
                var itemStack = ServerContext.ItemStackFactory.Create(itemId, count);
                return itemStack;
            }
            
            return _itemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            
            var remainingItems = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                var remaining = InsertItem(itemStack);
                if (remaining.Count > 0)
                {
                    remainingItems.Add(remaining);
                }
            }
            return remainingItems;
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            
            // 必要なアイテムのみ受け入れ可能とする
            // Only required items can be accepted
            var acceptableStacks = itemStacks.Where(s => _requiredItems.ContainsKey(s.Id)).ToList();
            if (acceptableStacks.Count == 0) return false;
            
            return _itemDataStoreService.InsertionCheck(acceptableStacks);
        }
        
        public int GetSlotSize()
        {
            CheckDestroy(this);
            return _itemDataStoreService.GetSlotSize();
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            CheckDestroy(this);
            return _itemDataStoreService.CreateCopiedItems();
        }
        
        #endregion
        
        #region IBlockSaveState
        
        public string SaveKey { get; } = typeof(BaseCampComponent).FullName;
        
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
        
        #endregion
        
        #region IBlockComponent
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        #endregion
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }
    }
}