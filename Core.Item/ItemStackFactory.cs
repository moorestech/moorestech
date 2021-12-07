using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;

namespace Core.Item
{
    public class ItemStackFactory
    {
        private readonly IItemConfig _itemConfig;

        public ItemStackFactory(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
        }

        public IItemStack Create(int id, int amount)
        {
            if (id == ItemConst.NullItemId)
            {
                return CreatEmpty();
            }
            if (amount < 1)
            {
                return CreatEmpty();
            }

            return new ItemStack(id, amount,_itemConfig,this);
        }
        public IItemStack CreatEmpty()
        {
            return new NullItemStack(this);
        }
    }
}