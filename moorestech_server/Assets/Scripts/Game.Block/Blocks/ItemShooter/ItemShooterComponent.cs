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
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    public class ItemShooterComponent : IItemCollectableBeltConveyor, IBlockInventory
    {
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private ShooterInventoryItem[] _inventoryItems;
        
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private readonly BlockDirection _blockDirection;
        private ItemShooterConfigParam _configParam;
        
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
                    
                    if (_blockConnectorComponent.ConnectTargets.Count == 0) continue;
                    
                    var connector = _blockConnectorComponent.ConnectTargets.First();
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
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;
                
                _inventoryItems[i] = inventoryItem;
                return null;
            }
            
            return inventoryItem;
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
    
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        
        public float RemainingPercent { get; set; }
        
        public float CurrentSpeed { get; set; }
        
        public ShooterInventoryItem(int itemId, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
        }
    }
}