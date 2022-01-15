using System.Collections.Generic;
using MainGame.Network.Interface;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.Event
{
    public class BlockPlaceEvent : IAnalysisEventPacket
    {
        IChunkUpdateObserver _chunkUpdateObserver;

        public BlockPlaceEvent(IChunkUpdateObserver chunkUpdateObserver)
        {
            _chunkUpdateObserver = chunkUpdateObserver;
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
            _chunkUpdateObserver.UpdateBlock(new Vector2Int(x,y), blockId);
        }
    }
}