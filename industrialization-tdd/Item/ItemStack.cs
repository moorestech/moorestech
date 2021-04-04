using System;

namespace industrialization.Item
{
    public class ItemStack : IItemStack
    {
        public int ID => id;
        public int Amount => amount;
        private int id;
        private int amount;
        //TODO そのアイテムのスタック数以上に入れないようにする
        public ItemStack(int id,int amount)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException();
            }
            this.id = id;
            this.amount = amount;
        }
        public static IItemStack[] NewItemStackArray(int amount)
        {
            var itemArray = new NullItemStack[amount];
            return itemArray;
        }

        //TODO アイテムスタック数のif文を追加する
        public ItemProcessResult addItem(IItemStack receiveItemStack)
        {
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                return new ItemProcessResult(this, new NullItemStack());
            }

            if (((ItemStack) receiveItemStack).id == id)
            {
                amount += ((ItemStack) receiveItemStack).amount;
                return new ItemProcessResult(this,new NullItemStack());
            }
            else
            {
                return new ItemProcessResult(this, receiveItemStack);
            }
        }

        //TODO 多分この戻り値だと問題が発生するからいつか直す
        public IItemStack subItem(int subAmount)
        {
            if (0 < amount - subAmount)
            {
                return new ItemStack(id,amount-subAmount);
            }
            else if(0 == amount - subAmount)
            {
                return new NullItemStack();
            }
            else
            {
                return this;
            }
        }
    }
}