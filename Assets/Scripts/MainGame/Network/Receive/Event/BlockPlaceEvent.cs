using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.Network.Interface;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.Event
{
    public class BlockPlaceEvent : IAnalysisEventPacket
    {
        readonly ChunkUpdateEvent _chunkUpdateEvent;

        public BlockPlaceEvent(IChunkUpdateEvent chunkUpdateEvent)
        {
            _chunkUpdateEvent = chunkUpdateEvent as ChunkUpdateEvent;
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
            _chunkUpdateEvent.InvokeBlockUpdateEvent(new Vector2Int(x,y), blockId);
        }
    }
}