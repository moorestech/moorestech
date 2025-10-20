using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    public readonly struct ItemShooterComponentSettings
    {
        public ItemShooterComponentSettings(int inventoryItemNum, float initialShootSpeed, float itemShootSpeed, float acceleration, BeltConveyorSlopeType slopeType)
        {
            InventoryItemNum = inventoryItemNum;
            InitialShootSpeed = initialShootSpeed;
            ItemShootSpeed = itemShootSpeed;
            Acceleration = acceleration;
            SlopeType = slopeType;
        }

        public int InventoryItemNum { get; }
        public float InitialShootSpeed { get; }
        public float ItemShootSpeed { get; }
        public float Acceleration { get; }
        public BeltConveyorSlopeType SlopeType { get; }
    }

    public class ItemShooterComponent : IItemCollectableBeltConveyor, IBlockInventory, IBlockSaveState, IUpdatableBlockComponent
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private readonly ShooterInventoryItem[] _inventoryItems;

        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly ItemShooterComponentSettings _settings;
        private const float InsertItemInterval = 1f; // TODO to master
        
        private float _lastInsertElapsedTime = float.MaxValue;
        private float? _externalAcceleration;

        public ItemShooterComponent(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterComponentSettings settings)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _settings = settings;
            SlopeType = settings.SlopeType;
            
            _inventoryItems = new ShooterInventoryItem[_settings.InventoryItemNum];
        }

        public ItemShooterComponent(Dictionary<string, string> componentStates, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterComponentSettings settings) :
            this(blockConnectorComponent, settings)
        {
            var items = JsonConvert.DeserializeObject<List<ItemShooterItemJsonObject>>(componentStates[SaveKey]);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.ItemStackSave == null) continue;
                
                var id = MasterHolder.ItemMaster.GetItemId(item.ItemStackSave.ItemGuid);
                _inventoryItems[i] = new ShooterInventoryItem(id, ItemInstanceId.Create(), (float)item.CurrentSpeed)
                {
                    RemainingPercent = (float)items[i].RemainingPercent
                };
            }
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            _lastInsertElapsedTime += (float)GameUpdater.UpdateSecondTime;
            var count = _inventoryItems.Length;
            var deltaTime = (float)GameUpdater.UpdateSecondTime; // floatとdobuleが混在しているの気持ち悪いから改善したい
            var itemShootSpeed = _settings.ItemShootSpeed;
            var acceleration = _externalAcceleration ?? _settings.Acceleration;
            
            for (var i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;
                
                if (item.RemainingPercent <= 0)
                {
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                    
                    if (_blockConnectorComponent.ConnectedTargets.Count == 0) continue;
                    
                    var connector = _blockConnectorComponent.ConnectedTargets.First();
                    var target = connector.Key;
                    if (target is ItemShooterComponent shooter)
                    {
                        _inventoryItems[i] = shooter.InsertItemFromShooter(item);
                    }
                    else
                    {
                        var output = connector.Key.InsertItem(insertItem);
                        
                        //渡した結果がnullItemだったらそのアイテムを消す
                        if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[i] = null;
                    }
                    
                    continue;
                }
                
                //時間を減らす
                item.RemainingPercent -= deltaTime * itemShootSpeed * item.CurrentSpeed;
                item.RemainingPercent = Math.Clamp(item.RemainingPercent, 0, 1);
                
                // velocityを更新する
                item.CurrentSpeed += acceleration * deltaTime;
                item.CurrentSpeed = Mathf.Clamp(item.CurrentSpeed, 0, float.MaxValue);
            }

            _externalAcceleration = null;
        }
        
        private ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem)
        {
            BlockException.CheckDestroy(this);
            
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] = inventoryItem;
                _inventoryItems[i].RemainingPercent = 1;
                return null;
            }
            
            return inventoryItem;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            // インサート間隔をチェック
            if (_lastInsertElapsedTime < InsertItemInterval) return itemStack;
            
            // インサート可能なスロットに挿入
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.InitialShootSpeed);
                //挿入したのでアイテムを減らして返す
                _lastInsertElapsedTime = 0;
                return itemStack.SubItem(1);
            }
            
            return itemStack;
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            // 空きスロットがあるかどうか
            var nullCount = 0;
            foreach (var inventoryItem in _inventoryItems)
            {
                if (inventoryItem == null) nullCount++;
            }
            // 挿入可能スロットがない
            if (nullCount == 0) return false;
            
            // 挿入スロットが1個かどうか
            if (itemStacks.Count == 1 && itemStacks[0].Count == 1) return true;
            
            return false;
        }
        
        public IItemStack GetItem(int slot)
        {
            var itemStackFactory = ServerContext.ItemStackFactory;
            var item = _inventoryItems[slot];
            return item == null ? itemStackFactory.CreatEmpty() : itemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _inventoryItems[slot] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.InitialShootSpeed);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryItems.Length;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string SaveKey { get; } = typeof(ItemShooterComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var items = _inventoryItems.Select(item => new ItemShooterItemJsonObject(item)).ToList();
            return JsonConvert.SerializeObject(items);
        }

        public void SetExternalAcceleration(float acceleration)
        {
            _externalAcceleration = acceleration;
        }
    }
    
    public class ItemShooterItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStackSave;
        
        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        [JsonProperty("currentSpeed")] public double CurrentSpeed;
        
        public ItemShooterItemJsonObject(ShooterInventoryItem shooterInventoryItem)
        {
            if (shooterInventoryItem == null)
            {
                ItemStackSave = null;
                RemainingPercent = 0;
                CurrentSpeed = 0;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(shooterInventoryItem.ItemId, 1);
            ItemStackSave = new ItemStackSaveJsonObject(item);
            RemainingPercent = shooterInventoryItem.RemainingPercent;
            CurrentSpeed = shooterInventoryItem.CurrentSpeed;
        }
    }
}
