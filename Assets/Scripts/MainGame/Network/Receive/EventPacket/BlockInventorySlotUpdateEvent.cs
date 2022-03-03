using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockInventorySlotUpdateEvent : IAnalysisEventPacket
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryUpdateEvent;

        public BlockInventorySlotUpdateEvent(IBlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent as BlockInventoryUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var slot = bytes.MoveNextToGetInt();
            var itemId = bytes.MoveNextToGetInt();
            var itemCount = bytes.MoveNextToGetInt();
            var x = bytes.MoveNextToGetInt();
            var y = bytes.MoveNextToGetInt();
            
            _blockInventoryUpdateEvent.InvokeBlockInventorySlotUpdate(new Vector2Int(x,y), slot, itemId, itemCount);
        }
    }
}