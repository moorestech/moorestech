using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class CraftingInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent;

        public CraftingInventorySlotEventProtocol(ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent)
        {
            this.receiveCraftingInventoryEvent = receiveCraftingInventoryEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<CraftingInventoryUpdateEventMessagePack>(packet.ToArray());


            var item = new ItemStack(data.Item.Id, data.Item.Count);
            var craftResult = new ItemStack(data.CreatableItem.Id, data.CreatableItem.Count);


            receiveCraftingInventoryEvent.InvokeCraftingInventorySlotUpdate(
                new CraftingInventorySlotUpdateProperties(data.Slot, item, craftResult, data.IsCraftable)).Forget();
        }
    }
}