#nullable enable
using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item.Interface;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
    {
        public int Id { get; }
        public int Count { get; }
        public long ItemHash { get; }
        public ItemInstanceId ItemInstanceId { get; }
        private readonly Dictionary<string, ItemStackMetaData> _metaData;
        
        public ItemStack(int id, int count, Dictionary<string, ItemStackMetaData> metaData)
        {
            var config = InternalItemContext.ItemConfig;
            if (id == ItemConst.EmptyItemId) throw new ArgumentException("Item id cannot be null");
            if (count < 1) throw new ArgumentOutOfRangeException();
            if (config.GetItemConfig(id).MaxStack < count)
                throw new ArgumentOutOfRangeException($"アイテムスタック数の最大値を超えています ID:{id} Count:{count} MaxStack:{config.GetItemConfig(id).MaxStack}");
            
            _metaData = metaData;
            ItemInstanceId = ItemInstanceId.Create();
            ;
            ItemHash = config.GetItemConfig(id).ItemHash;
            Id = id;
            Count = count;
        }
        
        public ItemStack(int id, int count, ItemInstanceId instanceId, Dictionary<string, ItemStackMetaData> metaData) : this(id, count, metaData)
        {
            ItemInstanceId = instanceId;
        }
        
        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            var factory = InternalItemContext.ItemStackFactory;
            //加算するアイテムがnullならそのまま追加して返す
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                // インスタンスIDが同じだとベルトコンベアなどの輸送時に問題が生じるので、新しいインスタンスを生成する
                var newItem = factory.Create(Id, Count, _metaData);
                return new ItemProcessResult(newItem, factory.CreatEmpty());
            }
            
            // アイテムが同じでない場合は追加できない
            if (!Addable((ItemStack)receiveItemStack))
            {
                var newItem = factory.Create(Id, Count, _metaData);
                return new ItemProcessResult(newItem, receiveItemStack);
            }
            
            var config = InternalItemContext.ItemConfig;
            
            var newCount = ((ItemStack)receiveItemStack).Count + Count;
            var tmpStack = config.GetItemConfig(Id).MaxStack;
            
            //量が指定数より多かったらはみ出した分を返す
            if (tmpStack < newCount)
            {
                var tmpItem = factory.Create(Id, tmpStack, _metaData);
                var tmpReceive = factory.Create(Id, newCount - tmpStack, _metaData);
                
                return new ItemProcessResult(tmpItem, tmpReceive);
            }
            
            return new ItemProcessResult(factory.Create(Id, newCount), factory.CreatEmpty());
        }
        
        public IItemStack SubItem(int subCount)
        {
            var factory = InternalItemContext.ItemStackFactory;
            if (0 < Count - subCount) return factory.Create(Id, Count - subCount, _metaData);
            
            return factory.CreatEmpty();
        }
        
        public bool IsAllowedToAdd(IItemStack item)
        {
            var tmpStack = InternalItemContext.ItemConfig.GetItemConfig(Id).MaxStack;
            
            return (Id == item.Id || item.Id == ItemConst.EmptyItemId) &&
                   item.Count + Count <= tmpStack;
        }
        
        public bool IsAllowedToAddWithRemain(IItemStack item)
        {
            return Id == item.Id || item.Id == ItemConst.EmptyItemId;
        }
        
        public ItemStackMetaData GetMeta(string key)
        {
            return _metaData.GetValueOrDefault(key);
        }
        
        public bool TryGetMeta(string key, out ItemStackMetaData value)
        {
            return _metaData.TryGetValue(key, out value);
        }
        
        public IItemStack SetMeta(string key, ItemStackMetaData value)
        {
            var copiedMeta = new Dictionary<string, ItemStackMetaData>(_metaData)
            {
                [key] = value,
            };
            return new ItemStack(Id, Count, copiedMeta);
        }
        
        private bool Addable(ItemStack target)
        {
            return Id == target.Id && CompareMeta(target);
        }
        
        public override bool Equals(object? obj)
        {
            if (typeof(ItemStack) != obj?.GetType()) return false;
            var other = (ItemStack)obj;
            
            return Id == other.Id &&
                   Count == other.Count &&
                   ItemHash == other.ItemHash &&
                   CompareMeta(other);
        }
        
        private bool CompareMeta(ItemStack other)
        {
            if (_metaData.Count != other._metaData.Count) return false;
            
            foreach (var (key, value) in _metaData)
            {
                if (!other._metaData.TryGetValue(key, out var otherValue)) return false;
                if (!value.Equals(otherValue)) return false;
            }
            foreach (var (key, value) in other._metaData)
            {
                if (!_metaData.TryGetValue(key, out var otherValue)) return false;
                if (!value.Equals(otherValue)) return false;
            }
            
            return true;
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