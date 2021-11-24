using System;
using Core.Config.Item;

namespace Core.Item
{
    public class ItemStack : IItemStack
    {
        public int Id { get; }

        public int Amount { get; }

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
            if (ItemConfig.GetItemConfig(id).Stack < amount)
            {
                throw new ArgumentOutOfRangeException();
            }
            Id = id;
            Amount = amount;
        }
        
        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            //加算するアイテムがnullならそのまま返す
            if (receiveItemStack.GetType() == typeof(NullItemStack))
            {
                return new ItemProcessResult(this, new NullItemStack());
            }
            //IDが違うならそれぞれで返す
            if (((ItemStack) receiveItemStack).Id != Id)
            {
                return new ItemProcessResult(this, receiveItemStack);
            }
            
            var newAmount = ((ItemStack) receiveItemStack).Amount + Amount;
            var tmpStack = ItemConfig.GetItemConfig(Id).Stack;
            
            //量が指定数より多かったらはみ出した分を返す
            if (tmpStack < newAmount)
            {
                var tmpItem = ItemStackFactory.NewItemStack(Id,tmpStack);
                var tmpReceive = ItemStackFactory.NewItemStack(Id, newAmount-tmpStack);

                return new ItemProcessResult(tmpItem, tmpReceive);
            }
            
            return new ItemProcessResult(new ItemStack(Id, newAmount), new NullItemStack());
        }

        //TODO 0以下になるテストを書く
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

        public bool IsAllowedToAdd(IItemStack item)
        {
            var tmpStack = ItemConfig.GetItemConfig(Id).Stack;
            
            return (Id == item.Id || item.Id == ItemConst.NullItemId)&&
                   item.Amount + Amount <=tmpStack;
        }

        public override bool Equals(object? obj)
        {
            if (typeof(ItemStack) != obj.GetType()) return false;
            return ((ItemStack) obj).Id == Id && ((ItemStack) obj).Amount == Amount;
        }

        public override string ToString()
        {
            return $"ID:{Id} Amount:{Amount}";
        }
    }
}