using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockRemoveEvent : IAnalysisEventPacket 
    {
        private readonly NetworkReceivedChunkDataEvent _networkReceivedChunkDataEvents;

        public BlockRemoveEvent(INetworkReceivedChunkDataEvent networkReceivedChunkDataEvents)
        {
            _networkReceivedChunkDataEvents = networkReceivedChunkDataEvents as NetworkReceivedChunkDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var x = bytes.MoveNextToGetInt();
            var y = bytes.MoveNextToGetInt();
            
            _networkReceivedChunkDataEvents.InvokeBlockUpdateEvent(
                new OnBlockUpdateEventProperties(new Vector2Int(x,y),BlockConstant.NullBlockId,BlockDirection.North));
        }
    }
}