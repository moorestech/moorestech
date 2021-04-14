using System;

namespace industrialization.Item
{
    public class ItemStack : IItemStack
    {
        public int Id { get; }

        public int Amount { get; }

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
            Id = id;
            Amount = amount;
        }

        //TODO アイテムスタック数のif文を追加する
        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                return new ItemProcessResult(this, new NullItemStack());
            }

            if (((ItemStack) receiveItemStack).Id == Id)
            {
                var newAmount = ((ItemStack) receiveItemStack).Amount + Amount;
                return new ItemProcessResult(new ItemStack(Id,newAmount),new NullItemStack());
            }
            else
            {
                return new ItemProcessResult(this, receiveItemStack);
            }
        }

        //TODO 多分この戻り値だと問題が発生するからいつか直す
        public IItemStack SubItem(int subAmount)
        {
            if (0 < Amount - subAmount)
            {
                return new ItemStack(Id,Amount-subAmount);
            }
            else if(0 == Amount - subAmount)
            {
                return new NullItemStack();
            }
            else
            {
                return this;
            }
        }

        public bool CanAdd(IItemStack item)
        {
            return Id == item.Id || item.Id == NullItemStack.NullItemId;
        }

        public override bool Equals(object? obj)
        {
            if (typeof(ItemStack) != obj.GetType()) return false;
            return ((ItemStack) obj).Id == Id && ((ItemStack) obj).Amount == Amount;
        }
    }
}