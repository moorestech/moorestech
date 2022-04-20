using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.Network.Util;

namespace MainGame.Model.Network.Receive.EventPacket
{
    public class CraftingInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly CraftingInventoryUpdateEvent _craftingInventoryUpdateEvent;
        public CraftingInventorySlotEventProtocol(CraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            _craftingInventoryUpdateEvent = craftingInventoryUpdateEvent;
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