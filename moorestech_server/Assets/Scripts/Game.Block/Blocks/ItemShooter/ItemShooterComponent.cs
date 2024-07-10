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
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    public class ItemShooterComponent : IItemCollectableBeltConveyor, IBlockInventory, IBlockSaveState
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly ItemShooterConfigParam _configParam;
        private readonly ShooterInventoryItem[] _inventoryItems;
        private readonly IDisposable _updateObservable;
        
        public ItemShooterComponent(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterConfigParam configParam)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _configParam = configParam;
            
            _inventoryItems = new ShooterInventoryItem[_configParam.InventoryItemNum];
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public ItemShooterComponent(string state, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ItemShooterConfigParam configParam) :
            this(blockConnectorComponent, configParam)
        {
            if (state == string.Empty) return;
            
            List<ItemShooterItemJsonObject> items = JsonConvert.DeserializeObject<List<ItemShooterItemJsonObject>>(state);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.ItemStack == null) continue;
                
                var id = ServerContext.ItemConfig.GetItemId(item.ItemStack.ItemHash);
                _inventoryItems[i] = new ShooterInventoryItem(id, ItemInstanceId.Create(), (float)item.CurrentSpeed)
                {
                    RemainingPercent = (float)items[i].RemainingPercent,
                };
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            //新しく挿入可能か
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _configParam.InitialShootSpeed);
                return itemStack.SubItem(1);
            }
            
            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
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
            _inventoryItems[slot] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _configParam.InitialShootSpeed);
        }
        
        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryItems.Length;
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var items = _inventoryItems.Select(item => new ItemShooterItemJsonObject(item)).ToList();
            return JsonConvert.SerializeObject(items);
        }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
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
                    
                    KeyValuePair<IBlockInventory, (IConnectOption selfOption, IConnectOption targetOption)> connector = _blockConnectorComponent.ConnectedTargets.First();
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
                var deltaTime = (float)GameUpdater.UpdateSecondTime; // floatとdobuleが混在しているの気持ち悪いから改善したい
                item.RemainingPercent -= deltaTime * _configParam.ItemShootSpeed * item.CurrentSpeed;
                item.RemainingPercent = Mathf.Clamp(item.RemainingPercent, 0, 1);
                
                // velocityを更新する
                item.CurrentSpeed += _configParam.Acceleration * deltaTime;
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
                _inventoryItems[i].RemainingPercent = 1;
                return null;
            }
            
            return inventoryItem;
        }
    }
    
    public class ItemShooterItemJsonObject
    {
        [JsonProperty("currentSpeed")]
        public double CurrentSpeed;
        [JsonProperty("itemStack")]
        public ItemStackJsonObject ItemStack;
        
        [JsonProperty("remainingTime")]
        public double RemainingPercent;
        
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