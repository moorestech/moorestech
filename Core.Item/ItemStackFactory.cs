using Core.Const;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;

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
            if (id == ItemConst.EmptyItemId)
            {
                return CreatEmpty();
            }

            if (count < 1)
            {
                return CreatEmpty();
            }

            return new ItemStack(id, count, _itemConfig, this);
        }
        public IItemStack Create(ulong itemHash, int count)
        {
            if (count < 1)
            {
                return CreatEmpty();
            }
            var config = _itemConfig.GetItemConfig(itemHash);

            return new ItemStack(, count, _itemConfig, this);
        }

        public IItemStack CreatEmpty()
        {
            return _nullItem;
        }
    }
}