using System;

namespace Game.PlayerInventory.Interface.Event
{
    public interface ICraftingEvent
    {
        //作られるアイテム数はアイテムの最大スタックを超えることがあるのでIItemStackではなくintを使う
        public void Subscribe(Action<(int itemId, int itemCount)> onCraft);
    }
}