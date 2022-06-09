using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;

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
            
            var data = MessagePackSerializer
                .Deserialize<MainInventoryUpdateEventMessagePack>(packet.ToArray());

            _mainInventoryUpdateEvent.InvokeMainInventorySlotUpdate(
                new MainInventorySlotUpdateProperties(
                    data.Slot,new ItemStack(data.Item.Id,data.Item.Count)));
        }
    }
}