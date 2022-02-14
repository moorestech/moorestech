using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockPlaceEvent : IAnalysisEventPacket
    {
        readonly NetworkReceivedChunkDataEvent _networkReceivedChunkDataEvent;

        public BlockPlaceEvent(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent)
        {
            _networkReceivedChunkDataEvent = networkReceivedChunkDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var x = bytes.MoveNextToGetInt();
            var y = bytes.MoveNextToGetInt();
            var blockId = bytes.MoveNextToGetInt();
            
            //ブロックをセットする
            _networkReceivedChunkDataEvent.InvokeBlockUpdateEvent(new OnBlockUpdateEventProperties(new Vector2Int(x,y), blockId));
        }
    }
}