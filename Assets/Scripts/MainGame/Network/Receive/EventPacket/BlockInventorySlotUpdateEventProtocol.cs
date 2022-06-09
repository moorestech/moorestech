using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockInventorySlotUpdateEventProtocol : IAnalysisEventPacket
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryUpdateEvent;

        public BlockInventorySlotUpdateEventProtocol(BlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {

            var data = MessagePackSerializer
                .Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(packet.ToArray());
            
            _blockInventoryUpdateEvent.InvokeBlockInventorySlotUpdate(
                new Vector2Int(data.X,data.Y), data.Slot, data.Item.Id, data.Item.Count);
        }
    }
}