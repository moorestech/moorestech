using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;

namespace MainGame.Network.Receive.EventPacket
{
    public class CraftingInventorySlotEvent : IAnalysisEventPacket
    {
        private readonly CraftingInventoryUpdateEvent _craftingInventoryUpdateEvent;
        public CraftingInventorySlotEvent(ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            _craftingInventoryUpdateEvent = craftingInventoryUpdateEvent as CraftingInventoryUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var slot = bytes.MoveNextToGetInt();
            var itemId = bytes.MoveNextToGetInt();
            var itemCount = bytes.MoveNextToGetInt();
            var craftResultId = bytes.MoveNextToGetInt();
            var craftResultCount = bytes.MoveNextToGetInt();
            var canCraft = bytes.MoveNextToGetByte() == 1;
            
            var item = new ItemStack(itemId, itemCount);
            var craftResult = new ItemStack(craftResultId, craftResultCount);
            
            
            _craftingInventoryUpdateEvent.InvokeCraftingInventorySlotUpdate(
                new CraftingInventorySlotUpdateProperties(slot,item,craftResult,canCraft));
        }
    }
}