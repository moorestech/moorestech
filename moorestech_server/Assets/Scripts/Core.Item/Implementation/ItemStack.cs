﻿#nullable enable
using System;
using Core.Const;
using Core.Item.Config;
using Core.Item.Interface;
using Core.Item.Interface.Config;
using Core.Item.Util;
using Game.Context;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
    {
        private readonly IItemConfig _itemConfig;
        private readonly IItemStackFactory _itemStackFactory;

        public ItemStack(int id, int count, IItemConfig itemConfig, IItemStackFactory itemStackFactory)
        {
            _itemConfig = itemConfig;
            _itemStackFactory = itemStackFactory;
            ItemInstanceId = ItemInstanceIdGenerator.Generate();
            ItemHash = itemConfig.GetItemConfig(id).ItemHash;
            if (id == ItemConst.EmptyItemId) throw new ArgumentException("Item id cannot be null");

            if (count < 1) throw new ArgumentOutOfRangeException();

            if (itemConfig.GetItemConfig(id).MaxStack < count)
                throw new ArgumentOutOfRangeException("アイテムスタック数の最大値を超えています ID:" + id + " Count:" + count +
                                                      " MaxStack:" + itemConfig.GetItemConfig(id).MaxStack);

            Id = id;
            Count = count;
        }

        public ItemStack(int id, int count, IItemConfig itemConfig, IItemStackFactory itemStackFactory, long instanceId)
            : this(id, count, itemConfig, itemStackFactory)
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
                var newItem = _itemStackFactory.Create(Id, Count);
                return new ItemProcessResult(newItem, _itemStackFactory.CreatEmpty());
            }

            //IDが違うならそれぞれで返す
            if (((ItemStack)receiveItemStack).Id != Id)
            {
                var newItem = _itemStackFactory.Create(Id, Count);
                return new ItemProcessResult(newItem, receiveItemStack);
            }

            var newCount = ((ItemStack)receiveItemStack).Count + Count;
            var tmpStack = _itemConfig.GetItemConfig(Id).MaxStack;

            //量が指定数より多かったらはみ出した分を返す
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
            if (typeof(ItemStack) != obj?.GetType()) return false;
            return ((ItemStack)obj).Id == Id && ((ItemStack)obj).Count == Count;
        }

        protected bool Equals(ItemStack other)
        {
            return Equals(_itemConfig, other._itemConfig) && Equals(_itemStackFactory, other._itemStackFactory) &&
                   Id == other.Id && Count == other.Count && ItemHash == other.ItemHash &&
                   ItemInstanceId == other.ItemInstanceId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_itemConfig, _itemStackFactory, Id, Count, ItemHash, ItemInstanceId);
        }

        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}