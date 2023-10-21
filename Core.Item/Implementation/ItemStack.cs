using System;
using Core.Const;
using Core.Item.Config;
using Core.Item.Util;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
    {
        private readonly IItemConfig _itemConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public ItemStack(int id, int count, IItemConfig itemConfig, ItemStackFactory itemStackFactory)
        {
            _itemConfig = itemConfig;
            _itemStackFactory = itemStackFactory;
            ItemInstanceId = ItemInstanceIdGenerator.Generate();
            ItemHash = itemConfig.GetItemConfig(id).ItemHash;
            if (id == ItemConst.EmptyItemId) throw new ArgumentException("Item id cannot be null");

            if (count < 1) throw new ArgumentOutOfRangeException();

            if (itemConfig.GetItemConfig(id).MaxStack < count) throw new ArgumentOutOfRangeException(" ID:" + id + " Count:" + count + " MaxStack:" + itemConfig.GetItemConfig(id).MaxStack);

            Id = id;
            Count = count;
        }

        public ItemStack(int id, int count, IItemConfig itemConfig, ItemStackFactory itemStackFactory, long instanceId) : this(id, count, itemConfig, itemStackFactory)
        {
            ItemInstanceId = instanceId;
        }

        public int Id { get; }
        public int Count { get; }
        public ulong ItemHash { get; }
        public long ItemInstanceId { get; }

        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            //null
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                // ID
                var newItem = _itemStackFactory.Create(Id, Count);
                return new ItemProcessResult(newItem, _itemStackFactory.CreatEmpty());
            }

            //ID
            if (((ItemStack)receiveItemStack).Id != Id)
            {
                var newItem = _itemStackFactory.Create(Id, Count);
                return new ItemProcessResult(newItem, receiveItemStack);
            }

            var newCount = ((ItemStack)receiveItemStack).Count + Count;
            var tmpStack = _itemConfig.GetItemConfig(Id).MaxStack;

            
            if (tmpStack < newCount)
            {
                var tmpItem = _itemStackFactory.Create(Id, tmpStack);
                var tmpReceive = _itemStackFactory.Create(Id, newCount - tmpStack);

                return new ItemProcessResult(tmpItem, tmpReceive);
            }

            return new ItemProcessResult(_itemStackFactory.Create(Id, newCount), _itemStackFactory.CreatEmpty());
        }

        public IItemStack SubItem(int subCount)
        {
            if (0 < Count - subCount) return _itemStackFactory.Create(Id, Count - subCount);

            return _itemStackFactory.CreatEmpty();
        }

        public bool IsAllowedToAdd(IItemStack item)
        {
            var tmpStack = _itemConfig.GetItemConfig(Id).MaxStack;

            return (Id == item.Id || item.Id == ItemConst.EmptyItemId) &&
                   item.Count + Count <= tmpStack;
        }

        public bool IsAllowedToAddWithRemain(IItemStack item)
        {
            return Id == item.Id || item.Id == ItemConst.EmptyItemId;
        }


        public override bool Equals(object? obj)
        {
            if (typeof(ItemStack) != obj.GetType()) return false;
            return ((ItemStack)obj).Id == Id && ((ItemStack)obj).Count == Count;
        }

        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}