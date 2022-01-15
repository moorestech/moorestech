using System.Collections.Generic;
using MainGame.GameLogic.Interface;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive.Event
{
    public class BlockPlaceEvent : IAnalysisEventPacket
    {
        IChunkDataStore _chunkDataStore;

        public BlockPlaceEvent(IChunkDataStore chunkDataStore)
        {
            _chunkDataStore = chunkDataStore;
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
            _chunkDataStore.SetBlock(new Vector2Int(x,y), blockId);
        }
    }
}