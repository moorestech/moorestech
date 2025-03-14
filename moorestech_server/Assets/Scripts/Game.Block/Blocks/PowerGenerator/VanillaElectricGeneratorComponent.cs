using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGeneratorComponent : IElectricGenerator, IBlockInventory, IOpenableInventory, IBlockSaveState, IUpdatableBlockComponent
    {
        private readonly BlockComponentManager _blockComponentManager = new();
        private readonly Dictionary<ItemId, FuelItemsElement> _fuelSettings;
        
        private readonly ElectricPower _infinityPower;
        private readonly bool _isInfinityPower;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        private ItemId _currentFuelItemId = ItemMaster.EmptyItemId;
        private double _remainingFuelTime;
        
        public VanillaElectricGeneratorComponent(VanillaPowerGeneratorProperties data)
        {
            BlockPositionInfo = data.BlockPositionInfo;
            BlockInstanceId = data.BlockInstanceId;
            _fuelSettings = data.FuelSettings;
            _isInfinityPower = data.IsInfinityPower;
            _infinityPower = data.InfinityPower;
            
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, data.FuelItemSlot);
            
            _blockComponentManager.AddComponent(data.InventoryInputConnectorComponent);
        }
        
        public VanillaElectricGeneratorComponent(Dictionary<string, string> componentStates, VanillaPowerGeneratorProperties data) : this(data)
        {
            var saveData = JsonConvert.DeserializeObject<VanillaElectricGeneratorSaveJsonObject>(componentStates[SaveKey]);
            
            var itemId = MasterHolder.ItemMaster.GetItemId(saveData.CurrentFuelItemGuid);
            _currentFuelItemId = itemId;
            _remainingFuelTime = saveData.RemainingFuelTime;
            
            for (var i = 0; i < saveData.Items.Count; i++)
            {
                _itemDataStoreService.SetItem(i, saveData.Items[i].ToItemStack());
            }
        }
        public BlockPositionInfo BlockPositionInfo { get; }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _itemDataStoreService.GetSlotSize();
        }
        
        public string SaveKey { get; } = typeof(VanillaElectricGeneratorComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(_currentFuelItemId).ItemGuid;
            var saveData = new VanillaElectricGeneratorSaveJsonObject
            {
                CurrentFuelItemGuidStr = itemGuid.ToString(),
                RemainingFuelTime = _remainingFuelTime,
                Items = _itemDataStoreService.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            
            return JsonConvert.SerializeObject(saveData);
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        
        public bool IsDestroy { get; private set; }
        
        public ElectricPower OutputEnergy()
        {
            BlockException.CheckDestroy(this);
            
            if (_isInfinityPower) return _infinityPower;
            if (_fuelSettings.TryGetValue(_currentFuelItemId, out var fuelSetting))
            {
                return (ElectricPower)fuelSetting.Power;
            }
            
            return new ElectricPower(0);
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _itemDataStoreService.CreateCopiedItems();
        }
        
        
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemId, count);
        }
        
        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemId, count);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        
        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            
            _itemDataStoreService.SetItem(slot, itemId, count);
        }
        
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            return _itemDataStoreService.ReplaceItem(slot, itemStack);
        }
        
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            //現在燃料を消費しているか判定
            //燃料が在る場合は燃料残り時間をUpdate時間分減らす
            if (_currentFuelItemId != ItemMaster.EmptyItemId)
            {
                _remainingFuelTime -= GameUpdater.UpdateSecondTime;
                
                //残り時間が0以下の時は燃料の設定をNullItemIdにする
                if (_remainingFuelTime <= 0) _currentFuelItemId = ItemMaster.EmptyItemId;
                
                return;
            }
            
            //燃料がない場合はスロットに燃料が在るか判定する
            //スロットに燃料がある場合は燃料の設定し、アイテムを1個減らす
            for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
            {
                //スロットに燃料がある場合
                var slotItemId = _itemDataStoreService.InventoryItems[i].Id;
                if (!_fuelSettings.ContainsKey(slotItemId)) continue;
                
                //ID、残り時間を設定
                _currentFuelItemId = MasterHolder.ItemMaster.GetItemId(_fuelSettings[slotItemId].ItemGuid);
                _remainingFuelTime = _fuelSettings[slotItemId].Time;
                
                //アイテムを1個減らす
                _itemDataStoreService.SetItem(i, _itemDataStoreService.InventoryItems[i].SubItem(1));
                return;
            }
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }
    }
    
    public class VanillaElectricGeneratorSaveJsonObject
    {
        [JsonProperty("currentFuelItemGuid")]
        public string CurrentFuelItemGuidStr;
        [JsonIgnore] public Guid CurrentFuelItemGuid => Guid.Parse(CurrentFuelItemGuidStr);
        
        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;
        
        [JsonProperty("remainingFuelTime")]
        public double RemainingFuelTime;
    }
}