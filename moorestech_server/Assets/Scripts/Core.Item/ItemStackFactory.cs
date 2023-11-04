using Core.Const;
using Core.Item.Config;
using Core.Item.Implementation;

namespace Core.Item
{
    public class ItemStackFactory
    {
        private readonly IItemConfig _itemConfig;
        private readonly IItemStack _nullItem;

        public ItemStackFactory(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _nullItem = new NullItemStack(this);
        }

        public IItemStack Create(int id, int count)
        {
            if (id == ItemConst.EmptyItemId) return CreatEmpty();

            if (count < 1) return CreatEmpty();

            return new ItemStack(id, count, _itemConfig, this);
        }

        public IItemStack Create(int id, int count, long instanceId)
        {
            if (id == ItemConst.EmptyItemId) return CreatEmpty();

            if (count < 1) return CreatEmpty();

            return new ItemStack(id, count, _itemConfig, this, instanceId);
        }

        public IItemStack Create(long itemHash, int count)
        {
            if (count < 1) return CreatEmpty();

            var id = _itemConfig.GetItemId(itemHash);
            if (id == ItemConst.EmptyItemId) return CreatEmpty();

            return new ItemStack(id, count, _itemConfig, this);
        }

        public IItemStack CreatEmpty()
        {
            return _nullItem;
        }

        public IItemStack Create(string modId, string itemName, int count)
        {
            var id = _itemConfig.GetItemId(modId, itemName);
            return Create(id, count);
        }
    }
}