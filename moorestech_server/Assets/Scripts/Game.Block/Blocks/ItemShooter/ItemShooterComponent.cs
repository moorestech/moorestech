using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ItemShooter
{
    // シューター本体の薄いラッパー // Thin wrapper for shooter behaviour delegating to service
    public class ItemShooterComponent : IItemCollectableBeltConveyor, IBlockInventory, IBlockSaveState, IUpdatableBlockComponent, IItemShooterComponent
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _service.BeltConveyorItems;

        private readonly ItemShooterComponentService _service;

        public ItemShooterComponent(ItemShooterComponentService service)
        {
            _service = service;
            SlopeType = service.SlopeType;
        }

        public ItemShooterComponent(Dictionary<string, string> componentStates, ItemShooterComponentService service) : this(service)
        {
            var items = JsonConvert.DeserializeObject<List<ItemShooterItemJsonObject>>(componentStates[SaveKey]);
            for (var i = 0; i < items.Count && i < _service.SlotSize; i++)
            {
                var item = items[i];
                if (item.ItemStackSave == null) continue;

                var id = MasterHolder.ItemMaster.GetItemId(item.ItemStackSave.ItemGuid);

                // 秒数からtickに変換して復元
                // Convert seconds to ticks for restoration
                var totalTicks = GameUpdater.SecondsToTicks(item.TotalSeconds);
                var remainingTicks = GameUpdater.SecondsToTicks(item.RemainingSeconds);
                var shooterItem = new ShooterInventoryItem(id, ItemInstanceId.Create(), totalTicks, null, null)
                {
                    RemainingTicks = remainingTicks
                };
                _service.SetSlot(i, shooterItem);
            }
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _service.Update();
        }

        public ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem)
        {
            BlockException.CheckDestroy(this);
            return _service.InsertItemFromShooter(inventoryItem);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _service.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _service.InsertionCheck(itemStacks);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return _service.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _service.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _service.SlotSize;
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
            var items = _service.EnumerateInventoryItems().Select(item => new ItemShooterItemJsonObject(item)).ToList();
            return JsonConvert.SerializeObject(items);
        }
    }

    public class ItemShooterItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStackSave;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;
        [JsonProperty("totalSeconds")] public double TotalSeconds;

        public ItemShooterItemJsonObject(ShooterInventoryItem shooterInventoryItem)
        {
            if (shooterInventoryItem == null)
            {
                ItemStackSave = null;
                RemainingSeconds = 0;
                TotalSeconds = 0;
                return;
            }

            var item = ServerContext.ItemStackFactory.Create(shooterInventoryItem.ItemId, 1);
            ItemStackSave = new ItemStackSaveJsonObject(item);

            // tickを秒数に変換して保存
            // Convert ticks to seconds for storage
            RemainingSeconds = GameUpdater.TicksToSeconds(shooterInventoryItem.RemainingTicks);
            TotalSeconds = GameUpdater.TicksToSeconds(shooterInventoryItem.TotalTicks);
        }
    }
}
