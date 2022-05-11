using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;

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
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var slot = bytes.MoveNextToGetInt();
            var id = bytes.MoveNextToGetInt();
            var count = bytes.MoveNextToGetInt();
            
            _grabInventoryUpdateEvent.GrabInventoryUpdateEventInvoke(
                new GrabInventoryUpdateEventProperties(new ItemStack(id,count)));
            
        }
    }
}