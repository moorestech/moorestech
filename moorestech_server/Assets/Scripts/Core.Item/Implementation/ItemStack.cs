#nullable enable
using System;
using Core.Const;
using Core.Item.Interface;
using Core.Item.Util;
using Game.Context;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
    {
        public ItemStack(int id, int count)
        {
            ItemInstanceId = ItemInstanceIdGenerator.Generate();
            var configData = ServerContext.ItemConfig.GetItemConfig(id);
            ItemHash = configData.ItemHash;
            if (id == ItemConst.EmptyItemId) throw new ArgumentException("Item id cannot be null");

            if (count < 1) throw new ArgumentOutOfRangeException();

            if (configData.MaxStack < count)
                throw new ArgumentOutOfRangeException("アイテムスタック数の最大値を超えています ID:" + id + " Count:" + count +
                                                      " MaxStack:" + configData.MaxStack);

            Id = id;
            Count = count;
        }

        public ItemStack(int id, int count, long instanceId) : this(id, count)
        {
            ItemInstanceId = instanceId;
        }

        public int Id { get; }
        public int Count { get; }
        public long ItemHash { get; }
        public long ItemInstanceId { get; }

        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            //加算するアイテムがnullならそのまま追加して返す
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                // インスタンスIDが同じだとベルトコンベアなどの輸送時に問題が生じるので、新しいインスタンスを生成する
                var newItem = ServerContext.ItemStackFactory.Create(Id, Count);
                var empty = ServerContext.ItemStackFactory.CreatEmpty();
                return new ItemProcessResult(newItem, empty);
            }

            //IDが違うならそれぞれで返す
            if (((ItemStack)receiveItemStack).Id != Id)
            {
                var newItem = ServerContext.ItemStackFactory.Create(Id, Count);
                return new ItemProcessResult(newItem, receiveItemStack);
            }

            var newCount = ((ItemStack)receiveItemStack).Count + Count;
            var tmpStack = ServerContext.ItemConfig.GetItemConfig(Id).MaxStack;

            //量が指定数より多かったらはみ出した分を返す
            if (tmpStack < newCount)
            {
                var tmpItem = ServerContext.ItemStackFactory.Create(Id, tmpStack);
                var tmpReceive = ServerContext.ItemStackFactory.Create(Id, newCount - tmpStack);

                return new ItemProcessResult(tmpItem, tmpReceive);
            }

            var reminderItem = ServerContext.ItemStackFactory.Create(Id, newCount);
            return new ItemProcessResult(reminderItem, ServerContext.ItemStackFactory.CreatEmpty());
        }

        public IItemStack SubItem(int subCount)
        {
            if (0 < Count - subCount) return ServerContext.ItemStackFactory.Create(Id, Count - subCount);

            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public bool IsAllowedToAdd(IItemStack item)
        {
            var tmpStack = ServerContext.ItemConfig.GetItemConfig(Id).MaxStack;

            return (Id == item.Id || item.Id == ItemConst.EmptyItemId) &&
                   item.Count + Count <= tmpStack;
        }

        public bool IsAllowedToAddWithRemain(IItemStack item)
        {
            return Id == item.Id || item.Id == ItemConst.EmptyItemId;
        }


        public override bool Equals(object? obj)
        {
            if (typeof(ItemStack) != obj?.GetType()) return false;
            return ((ItemStack)obj).Id == Id && ((ItemStack)obj).Count == Count;
        }

        protected bool Equals(ItemStack other)
        {
            return Id == other.Id && Count == other.Count && ItemHash == other.ItemHash &&
                   ItemInstanceId == other.ItemInstanceId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Count, ItemHash, ItemInstanceId);
        }

        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}