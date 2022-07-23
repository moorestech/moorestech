using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class GrabInventoryUpdateEventProtocol : IAnalysisEventPacket
    {
        
        private readonly ReceiveGrabInventoryEvent receiveGrabInventoryEvent;

        public GrabInventoryUpdateEventProtocol(ReceiveGrabInventoryEvent receiveGrabInventory)
        {
            receiveGrabInventoryEvent = receiveGrabInventory;
        }
        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<GrabInventoryUpdateEventMessagePack>(packet.ToArray());

            receiveGrabInventoryEvent.GrabInventoryUpdateEventInvoke(
                new GrabInventoryUpdateEventProperties(new ItemStack(data.Item.Id,data.Item.Count)));
            
        }
    }
}