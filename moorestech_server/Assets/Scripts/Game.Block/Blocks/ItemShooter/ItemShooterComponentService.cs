using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    /// <summary>
    /// シューター用設定値を保持するレコード
    /// Setting bundle for item shooter components
    /// </summary>
    public readonly struct ItemShooterComponentSettings
    {
        public int InventoryItemNum { get; }
        public float InitialShootSpeed { get; }
        public float ItemShootSpeed { get; }
        public float Acceleration { get; }
        public BeltConveyorSlopeType SlopeType { get; }
        
        public ItemShooterComponentSettings(ItemShooterAcceleratorBlockParam param)
        {
            var slope = param.SlopeType switch
            {
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                _ => BeltConveyorSlopeType.Straight
            };
            
            InventoryItemNum = param.InventoryItemNum;
            InitialShootSpeed = param.InitialShootSpeed;
            ItemShootSpeed = param.ItemShootSpeed;
            Acceleration = param.Acceleration;
            SlopeType = slope;
        }
        
        
        public ItemShooterComponentSettings(ItemShooterBlockParam param)
        {
            var slope = param.SlopeType switch
            {
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                _ => BeltConveyorSlopeType.Straight
            };
            
            InventoryItemNum = param.InventoryItemNum;
            InitialShootSpeed = param.InitialShootSpeed;
            ItemShootSpeed = param.ItemShootSpeed;
            Acceleration = param.Acceleration;
            SlopeType = slope;
        }
        
    }

    /// <summary>
    /// シューターの在庫管理と転送処理を共通化
    /// Shared inventory/transfer logic for shooter variants
    /// </summary>
    public class ItemShooterComponentService
    {
        public BeltConveyorSlopeType SlopeType => _settings.SlopeType;
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        public int SlotSize => _inventoryItems.Length;

        private readonly BlockConnectorComponent<IBlockInventory> _connectorComponent;
        private readonly ItemShooterComponentSettings _settings;
        private readonly ShooterInventoryItem[] _inventoryItems;

        private const float InsertItemInterval = 1f; // TODO to master
        private float _lastInsertElapsedTime = float.MaxValue;
        private float? _externalAcceleration;

        // 
        /// <summary>
        /// 依存関係と在庫スロットを初期化
        /// Initialize dependencies and slot buffer
        /// </summary>
        /// <param name="connectorComponent"></param>
        /// <param name="settings"></param>
        public ItemShooterComponentService(BlockConnectorComponent<IBlockInventory> connectorComponent, ItemShooterComponentSettings settings)
        {
            _connectorComponent = connectorComponent;
            _settings = settings;
            _inventoryItems = new ShooterInventoryItem[_settings.InventoryItemNum];
        }

        /// <summary>
        /// 更新処理で射出進行と速度を管理
        /// Update travel progress and velocity each frame
        /// </summary>
        public void Update(float deltaTime)
        {
            _lastInsertElapsedTime += deltaTime;

            var acceleration = _externalAcceleration ?? _settings.Acceleration;
            var itemShootSpeed = _settings.ItemShootSpeed;

            // スロットごとのアイテムを処理
            // Iterate slot-wise over conveyor items
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;

                // 完了済みアイテムを隣接接続へ転送
                // Transfer finished items to connected blocks
                if (item.RemainingPercent <= 0)
                {
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);

                    if (_connectorComponent.ConnectedTargets.Count == 0) continue;

                    var connector = _connectorComponent.ConnectedTargets.First();
                    var target = connector.Key;
                    if (target is IItemShooterComponent shooter)
                    {
                        _inventoryItems[i] = shooter.InsertItemFromShooter(item);
                    }
                    else
                    {
                        var output = connector.Key.InsertItem(insertItem);
                        if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[i] = null;
                    }

                    continue;
                }

                // 残り距離と速度を更新
                // Update remaining distance and velocity
                item.RemainingPercent -= deltaTime * itemShootSpeed * item.CurrentSpeed;
                item.RemainingPercent = Math.Clamp(item.RemainingPercent, 0, 1);

                item.CurrentSpeed += acceleration * deltaTime;
                item.CurrentSpeed = Mathf.Clamp(item.CurrentSpeed, 0, float.MaxValue);
            }

            _externalAcceleration = null;
        }

        public ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem)
        {
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
            if (_lastInsertElapsedTime < InsertItemInterval) return itemStack;

            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;

                _inventoryItems[i] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.InitialShootSpeed);
                _lastInsertElapsedTime = 0;
                return itemStack.SubItem(1);
            }

            return itemStack;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            var nullCount = 0;
            foreach (var inventoryItem in _inventoryItems)
            {
                if (inventoryItem == null) nullCount++;
            }

            if (nullCount == 0) return false;
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
            _inventoryItems[slot] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.InitialShootSpeed);
        }

        public void SetSlot(int slot, ShooterInventoryItem shooterInventoryItem)
        {
            _inventoryItems[slot] = shooterInventoryItem;
        }

        public IEnumerable<ShooterInventoryItem> EnumerateInventoryItems()
        {
            return _inventoryItems;
        }

        public void SetExternalAcceleration(float acceleration)
        {
            _externalAcceleration = acceleration;
        }
    }
}
