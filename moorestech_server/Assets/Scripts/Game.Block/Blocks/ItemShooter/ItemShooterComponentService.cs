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
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.ItemShooter
{
    /// <summary>
    /// シューター用設定値を保持するレコード
    /// Setting bundle for item shooter components
    /// </summary>
    public readonly struct ItemShooterComponentSettings
    {
        public int InventoryItemNum { get; }
        public uint TotalTicks { get; }
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
            SlopeType = slope;

            // ItemShootSpeedから総tick数を計算（1秒あたりの進行割合→通過秒数→tick数）
            // Calculate total ticks from ItemShootSpeed (progress per second -> transit seconds -> ticks)
            var transitSeconds = 1.0 / param.ItemShootSpeed;
            TotalTicks = GameUpdater.SecondsToTicks(transitSeconds);
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
            SlopeType = slope;

            // ItemShootSpeedから総tick数を計算
            // Calculate total ticks from ItemShootSpeed
            var transitSeconds = 1.0 / param.ItemShootSpeed;
            TotalTicks = GameUpdater.SecondsToTicks(transitSeconds);
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

        // アイテム挿入間隔（tick単位）
        // Item insertion interval in ticks
        private readonly uint _insertItemIntervalTicks = GameUpdater.SecondsToTicks(1); // TODO to master
        private uint _lastInsertElapsedTicks;

        /// <summary>
        /// 依存関係と在庫スロットを初期化
        /// Initialize dependencies and slot buffer
        /// </summary>
        public ItemShooterComponentService(BlockConnectorComponent<IBlockInventory> connectorComponent, ItemShooterComponentSettings settings)
        {
            _connectorComponent = connectorComponent;
            _settings = settings;
            _inventoryItems = new ShooterInventoryItem[_settings.InventoryItemNum];

            // 起動直後にアイテム挿入を許可するため、挿入間隔tickを初期値として設定
            // Initialize to insertion interval to allow immediate item insertion on startup
            _lastInsertElapsedTicks = _insertItemIntervalTicks;
        }

        private int _lastInsertSlotIndex = -1;

        /// <summary>
        /// 更新処理でtick進行を管理
        /// Update tick-based progress
        /// </summary>
        public void Update()
        {
            // 経過tick数を積算
            // Accumulate elapsed ticks for insertion interval
            UpdateElapsedTicks();

            // スロットごとのアイテムを処理
            // Iterate slot-wise over conveyor items
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                ProcessSlot(i);
            }

            #region Internal

            // 経過tick数を累積する（飽和加算でオーバーフロー防止）
            // Accumulate elapsed insertion ticks (saturating add to prevent overflow)
            void UpdateElapsedTicks()
            {
                var remaining = _insertItemIntervalTicks - _lastInsertElapsedTicks;
                if (_lastInsertElapsedTicks >= _insertItemIntervalTicks || 1u >= remaining)
                {
                    _lastInsertElapsedTicks = _insertItemIntervalTicks;
                }
                else
                {
                    _lastInsertElapsedTicks++;
                }
            }

            // 各スロットのアイテムを処理する
            // Process a single slot item
            void ProcessSlot(int slotIndex)
            {
                var item = _inventoryItems[slotIndex];
                if (item == null) return;

                if (item.RemainingTicks == 0)
                {
                    HandleFinishedItem(slotIndex, item);
                    return;
                }

                UpdateActiveItem(item);
            }

            // 完了したアイテムを隣接ブロックへ渡す
            // Hand over finished items to connected blocks
            void HandleFinishedItem(int slotIndex, ShooterInventoryItem finishedItem)
            {
                var insertItem = ServerContext.ItemStackFactory.Create(finishedItem.ItemId, 1, finishedItem.ItemInstanceId);

                if (_connectorComponent.ConnectedTargets.Count == 0) return;

                _lastInsertSlotIndex++;
                if (_lastInsertSlotIndex >= _connectorComponent.ConnectedTargets.Count)
                {
                    _lastInsertSlotIndex = 0;
                }
                var connector = _connectorComponent.ConnectedTargets.ElementAt(_lastInsertSlotIndex);
                var target = connector.Key;
                if (target is IItemShooterComponent shooter)
                {
                    _inventoryItems[slotIndex] = shooter.InsertItemFromShooter(finishedItem);
                }
                else
                {
                    var output = connector.Key.InsertItem(insertItem, InsertItemContext.Empty);
                    if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[slotIndex] = null;
                }
            }

            // 残りtick数を減らす
            // Decrease remaining ticks
            void UpdateActiveItem(ShooterInventoryItem activeItem)
            {
                if (activeItem.RemainingTicks > 0)
                {
                    activeItem.RemainingTicks--;
                }
            }

            #endregion
        }

        public ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem)
        {
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;

                _inventoryItems[i] = inventoryItem;
                _inventoryItems[i].RemainingTicks = _settings.TotalTicks;
                return null;
            }

            return inventoryItem;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            // tick単位で挿入間隔をチェック
            // Check insertion interval in ticks
            if (_lastInsertElapsedTicks < _insertItemIntervalTicks) return itemStack;

            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] != null) continue;

                _inventoryItems[i] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.TotalTicks, null, null);
                _lastInsertElapsedTicks = 0;
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
            _inventoryItems[slot] = new ShooterInventoryItem(itemStack.Id, itemStack.ItemInstanceId, _settings.TotalTicks, null, null);
        }

        public void SetSlot(int slot, ShooterInventoryItem shooterInventoryItem)
        {
            _inventoryItems[slot] = shooterInventoryItem;
        }

        public IEnumerable<ShooterInventoryItem> EnumerateInventoryItems()
        {
            return _inventoryItems;
        }
    }
}
