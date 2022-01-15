using System.Linq;
using MainGame.Constant;
using MainGame.GameLogic.Interface;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Receive
{
    public class ReceiveChunkDataProtocol : IAnalysisPacket
    {
        private readonly IChunkDataStore _chunkDataStore;

        public ReceiveChunkDataProtocol(IChunkDataStore chunkDataStore)
        {
            _chunkDataStore = chunkDataStore;
        }

        public void Analysis(byte[] data)
        {
            var bits = new BitListEnumerator(data.ToList());
            //packet id
            bits.MoveNextToShort();
            var x = bits.MoveNextToInt();
            var y = bits.MoveNextToInt();
            var chunkPos = new Vector2Int(x, y);

            var chunkBlocks = new int[ChunkConstant.ChunkSize,ChunkConstant.ChunkSize];
            
            //analysis block data
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    chunkBlocks[i, j] = GetBlockId(bits);
                }
            }
            
            //Set chunk data
            _chunkDataStore.SetChunk(chunkPos, chunkBlocks);
        }

        private int GetBlockId(BitListEnumerator bits)
        {
            var isBlock = bits.MoveNextToBit();
            if (isBlock)
            {
                var isInt = bits.MoveNextToBit();
                if (isInt)
                {
                    //block id type is int
                    bits.MoveNextToBit();
                    return bits.MoveNextToInt();
                }
                var isShort = bits.MoveNextToBit();
                if (isShort)
                {
                    //block id type is short
                    return bits.MoveNextToShort();
                }
                //block id type is byte
                return bits.MoveNextToByte();
            }
            //none block
            return BlockConstant.NullBlockId;
        }
    }
}