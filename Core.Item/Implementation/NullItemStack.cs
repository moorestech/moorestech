using Core.Const;
using Core.Item.Util;

namespace Core.Item.Implementation
{
    internal class NullItemStack : IItemStack
    {
        private readonly ItemStackFactory _itemStackFactory;

        public NullItemStack(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
            ItemInstanceId = ItemInstanceIdGenerator.Generate();
        }

        public int Id => ItemConst.EmptyItemId;
        public int Count => 0;
        public ulong ItemHash => 0;
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
            if (typeof(NullItemStack) != obj.GetType()) return false;
            return ((NullItemStack)obj).Id == Id && ((NullItemStack)obj).Count == Count;
        }

        public override string ToString()
        {
            return $"ID:{Id} Count:{Count}";
        }
    }
}