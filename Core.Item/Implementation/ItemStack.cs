using System;
using Core.Config.Item;
using Core.Item.Config;
using Core.Item.Util;

namespace Core.Item.Implementation
{
    internal class ItemStack : IItemStack
    {
        public int Id { get; }
        public int Amount { get; }
        private readonly IItemConfig _itemConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public ItemStack(int id,int amount,IItemConfig itemConfig,ItemStackFactory itemStackFactory)
        {
            _itemConfig = itemConfig;
            _itemStackFactory = itemStackFactory;
            if (id == ItemConst.NullItemId)
            {
                throw new ArgumentException("Item id cannot be null");
            }
            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (itemConfig.GetItemConfig(id).Stack < amount)
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
                return new ItemProcessResult(this, _itemStackFactory.CreatEmpty());
            }
            //IDが違うならそれぞれで返す
            if (((ItemStack) receiveItemStack).Id != Id)
            {
                return new ItemProcessResult(this, receiveItemStack);
            }
            
            var newAmount = ((ItemStack) receiveItemStack).Amount + Amount;
            var tmpStack = _itemConfig.GetItemConfig(Id).Stack;
            
            //量が指定数より多かったらはみ出した分を返す
            if (tmpStack < newAmount)
            {
                var tmpItem = _itemStackFactory.Create(Id,tmpStack);
                var tmpReceive = _itemStackFactory.Create(Id, newAmount-tmpStack);

                return new ItemProcessResult(tmpItem, tmpReceive);
            }
            
            return new ItemProcessResult(_itemStackFactory.Create(Id, newAmount), _itemStackFactory.CreatEmpty());
        }

        public IItemStack SubItem(int subAmount)
        {
            if (0 < Amount - subAmount)
            {
                return _itemStackFactory.Create(Id,Amount-subAmount);
            }
            return _itemStackFactory.CreatEmpty();
        }

        public bool IsAllowedToAdd(IItemStack item)
        {
            var tmpStack = _itemConfig.GetItemConfig(Id).Stack;
            
            return (Id == item.Id || item.Id == ItemConst.NullItemId)&&
                   item.Amount + Amount <=tmpStack;
        }

        public IItemStack Clone()
        {
            return _itemStackFactory.Create(Id,Amount);
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