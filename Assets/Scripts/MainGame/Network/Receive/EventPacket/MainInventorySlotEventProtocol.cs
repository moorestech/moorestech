using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.Network.Util;

namespace MainGame.Network.Receive.EventPacket
{
    public class MainInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;

        public MainInventorySlotEventProtocol(MainInventoryUpdateEvent mainInventorySlotEvent)
        {
            _mainInventoryUpdateEvent = mainInventorySlotEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var slot = bytes.MoveNextToGetInt();
            var id = bytes.MoveNextToGetInt();
            var count = bytes.MoveNextToGetInt();
            
            _mainInventoryUpdateEvent.InvokeMainInventorySlotUpdate(
                new MainInventorySlotUpdateProperties(
                    slot,new ItemStack(id,count)));
        }
    }
}