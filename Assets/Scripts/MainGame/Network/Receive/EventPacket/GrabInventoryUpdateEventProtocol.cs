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
        
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;

        public GrabInventoryUpdateEventProtocol(GrabInventoryUpdateEvent grabInventoryUpdate)
        {
            _grabInventoryUpdateEvent = grabInventoryUpdate;
        }
        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<GrabInventoryUpdateEventMessagePack>(packet.ToArray());

            _grabInventoryUpdateEvent.GrabInventoryUpdateEventInvoke(
                new GrabInventoryUpdateEventProperties(new ItemStack(data.Item.Id,data.Item.Count)));
            
        }
    }
}