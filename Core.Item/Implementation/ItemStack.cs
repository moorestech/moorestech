using System;
using Core.Config.Item;
using Core.Item.Util;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
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
                return new ItemProcessResult(this, ItemStackFactory.CreatEmpty());
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
                var tmpItem = ItemStackFactory.Create(Id,tmpStack);
                var tmpReceive = ItemStackFactory.Create(Id, newAmount-tmpStack);

                return new ItemProcessResult(tmpItem, tmpReceive);
            }
            
            return new ItemProcessResult(ItemStackFactory.Create(Id, newAmount), ItemStackFactory.CreatEmpty());
        }

        public IItemStack SubItem(int subAmount)
        {
            if (0 < Amount - subAmount)
            {
                return ItemStackFactory.Create(Id,Amount-subAmount);
            }
            return ItemStackFactory.CreatEmpty();
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