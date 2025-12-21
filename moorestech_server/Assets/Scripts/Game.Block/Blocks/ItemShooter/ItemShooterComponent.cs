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
                var shooterItem = new ShooterInventoryItem(id, ItemInstanceId.Create(), (float)item.CurrentSpeed, null)
                {
                    RemainingPercent = (float)items[i].RemainingPercent
                };
                _service.SetSlot(i, shooterItem);
            }
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _service.Update((float)GameUpdater.UpdateSecondTime);
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

        public void SetExternalAcceleration(float acceleration)
        {
            _service.SetExternalAcceleration(acceleration);
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
