using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    public class ItemShooterComponent : IItemCollectableBeltConveyor, IBlockInventory, IBlockSaveState
    {
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private readonly ShooterInventoryItem[] _inventoryItems;
        
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private readonly BlockDirection _blockDirection;
        private readonly ItemShooterConfigParam _configParam;
        
        private readonly IDisposable _updateObservable;
        
        public ItemShooterComponent(BlockDirection blockDirection, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterConfigParam configParam)
        {
            _blockDirection = blockDirection;
            _blockConnectorComponent = blockConnectorComponent;
            _configParam = configParam;
            
            _inventoryItems = new ShooterInventoryItem[_configParam.InventoryItemNum];
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public ItemShooterComponent(string state, BlockDirection blockDirection, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterConfigParam configParam):
            this(blockDirection, blockConnectorComponent, configParam)
        {
            if (state == string.Empty) return;
            
            var items = JsonConvert.DeserializeObject<List<ItemShooterItemJsonObject>>(state);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.ItemStack == null) continue;
                
                var id = ServerContext.ItemConfig.GetItemId(item.ItemStack.ItemHash);
                _inventoryItems[i] = new ShooterInventoryItem(id, ItemInstanceId.Create(), (float)item.CurrentSpeed)
                {
                    RemainingPercent = (float)items[i].RemainingPercent
                };
            }
        }
        
        private void Update()
        {
            BlockException.CheckDestroy(this);
            
            var count = _inventoryItems.Length;
            
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
                        if (output.Id == ItemConst.EmptyItemId) _inventoryItems[i] = null;
                    }
                    
                    continue;
                }
                
                //時間を減らす
                var deltaTime = (float)GameUpdater.UpdateMillSecondTime; // floatとdobuleが混在しているの気持ち悪いから改善したい
                item.RemainingPercent -= deltaTime * _configParam.ItemShootSpeed * item.CurrentSpeed;
                item.RemainingPercent = Mathf.Clamp(item.RemainingPercent, 0, 1);
                
                // velocityを更新する
                var acceleration = _configParam.GetAcceleration(_blockDirection);
                item.CurrentSpeed += acceleration * deltaTime;
                item.CurrentSpeed = Mathf.Clamp(item.CurrentSpeed, 0, float.MaxValue);
            }
        }
        
        private ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem)
        {
            BlockException.CheckDestroy(this);
            
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] = inventoryItem;
                return null;
            }
            
            return inventoryItem;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            //新しく挿入可能か
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] =  new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _configParam.InitialShootSpeed);
                return itemStack.SubItem(1);
            }
            
            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
        }
        
        public IItemStack GetItem(int slot)
        {
            var itemStackFactory = ServerContext.ItemStackFactory;
            var item = _inventoryItems[slot];
            return item == null ? 
                itemStackFactory.CreatEmpty() : 
                itemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
        }
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _inventoryItems[slot] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _configParam.InitialShootSpeed);
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
            _updateObservable.Dispose();
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var items = _inventoryItems.Select(item => new ItemShooterItemJsonObject(item)).ToList();
            return JsonConvert.SerializeObject(items);
        }
    }
    
    public class ItemShooterItemJsonObject
    {
        [JsonProperty("itemStack")]
        public ItemStackJsonObject ItemStack;
        
        [JsonProperty("remainingTime")]
        public double RemainingPercent;
        
        [JsonProperty("currentSpeed")]
        public double CurrentSpeed;
        
        public ItemShooterItemJsonObject(ShooterInventoryItem shooterInventoryItem)
        {
            if (shooterInventoryItem == null)
            {
                ItemStack = null;
                RemainingPercent = 0;
                CurrentSpeed = 0;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(shooterInventoryItem.ItemId, 1);
            ItemStack = new ItemStackJsonObject(item);
            RemainingPercent = shooterInventoryItem.RemainingPercent;
            CurrentSpeed = shooterInventoryItem.CurrentSpeed;
        }
    }
}