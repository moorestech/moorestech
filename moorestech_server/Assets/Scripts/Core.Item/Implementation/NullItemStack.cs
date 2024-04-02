#nullable enable
using System;
using Core.Const;
using Core.Item.Interface;
using Core.Item.Util;

namespace Core.Item.Implementation
{
    internal class NullItemStack : IItemStack
    {
        private readonly IItemStackFactory _itemStackFactory;

        public NullItemStack(IItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
            ItemInstanceId = ItemInstanceIdGenerator.Generate();
        }

        public int Id => ItemConst.EmptyItemId;
        public int Count => 0;
        public long ItemHash => 0;
        public long ItemInstanceId { get; }

        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            //そのまま足すとインスタスIDが同じになってベルトコンベアなどで運ぶときに問題が生じるので、新しいインスタンスを生成する
            var tmpItem = _itemStackFactory.Create(receiveItemStack.Id, receiveItemStack.Count);
            return new ItemProcessResult(tmpItem, _itemStackFactory.CreatEmpty());
        }

        public IItemStack SubItem(int subCount)
        {
            return this;
        }

        public bool IsAllowedToAdd(IItemStack item)
        {
            return true;
        }

        public bool IsAllowedToAddWithRemain(IItemStack item)
        {
            return true;
        }

        public IItemStack Clone()
        {
            return _itemStackFactory.CreatEmpty();
        }

        public override bool Equals(object? obj)
        {
            if (typeof(NullItemStack) != obj?.GetType()) return false;
            return ((NullItemStack)obj).Id == Id && ((NullItemStack)obj).Count == Count;
        }

        protected bool Equals(NullItemStack other)
        {
            return Equals(_itemStackFactory, other._itemStackFactory) && ItemInstanceId == other.ItemInstanceId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_itemStackFactory, ItemInstanceId);
        }

        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}