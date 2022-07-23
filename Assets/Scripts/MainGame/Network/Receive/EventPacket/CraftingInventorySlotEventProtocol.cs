using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class CraftingInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly ReciveCraftingInventoryEvent reciveCraftingInventoryEvent;
        public CraftingInventorySlotEventProtocol(ReciveCraftingInventoryEvent reciveCraftingInventoryEvent)
        {
            this.reciveCraftingInventoryEvent = reciveCraftingInventoryEvent;
        }

        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<CraftingInventoryUpdateEventMessagePack>(packet.ToArray());
            
            
            var item = new ItemStack(data.Item.Id, data.Item.Count);
            var craftResult = new ItemStack(data.CreatableItem.Id, data.CreatableItem.Count);
            
            
            reciveCraftingInventoryEvent.InvokeCraftingInventorySlotUpdate(
                new CraftingInventorySlotUpdateProperties(data.Slot,item,craftResult,data.IsCraftable));
        }
    }
}