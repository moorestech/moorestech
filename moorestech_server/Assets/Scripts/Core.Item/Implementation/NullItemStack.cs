#nullable enable
using System;
using Core.Item.Interface;
using Core.Master;

namespace Core.Item.Implementation
{
    internal class NullItemStack : IItemStack
    {
        public ItemId Id => ItemMaster.EmptyItemId;
        public int Count => 0;
        public long ItemHash => 0;
        public ItemInstanceId ItemInstanceId { get; }
        
        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            //そのまま足すとインスタスIDが同じになってベルトコンベアなどで運ぶときに問題が生じるので、新しいインスタンスを生成する
            var tmpItem = InternalItemContext.ItemStackFactory.Create(receiveItemStack.Id, receiveItemStack.Count);
            var empty = InternalItemContext.ItemStackFactory.CreatEmpty();
            return new ItemProcessResult(tmpItem, empty);
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
        
        public ItemStackMetaData GetMeta(string key)
        {
            //TODO ログに入れる
            throw new InvalidOperationException("空のアイテムにメタデータは存在しません");
        }
        
        public bool TryGetMeta(string key, out ItemStackMetaData value)
        {
            throw new InvalidOperationException("空のアイテムにメタデータは存在しません");
        }
        
        public IItemStack SetMeta(string key, ItemStackMetaData value)
        {
            throw new InvalidOperationException("空のアイテムにメタデータは入れられません");
        }
        
        public IItemStack Clone()
        {
            return InternalItemContext.ItemStackFactory.CreatEmpty();
        }
        
        public override bool Equals(object? obj)
        {
            if (typeof(NullItemStack) != obj?.GetType()) return false;
            return ((NullItemStack)obj).Id == Id && ((NullItemStack)obj).Count == Count;
        }
        
        protected bool Equals(NullItemStack other)
        {
            return ItemInstanceId == other.ItemInstanceId;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(ItemInstanceId);
        }
        
        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}