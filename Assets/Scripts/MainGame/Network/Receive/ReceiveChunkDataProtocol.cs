using System.Collections.Generic;
using System.Linq;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Model.Network.Receive
{
    public class ReceiveChunkDataProtocol : IAnalysisPacket
    {
        private readonly NetworkReceivedChunkDataEvent _networkReceivedChunkDataEvent;

        public ReceiveChunkDataProtocol(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent)
        {
            _networkReceivedChunkDataEvent = networkReceivedChunkDataEvent;
        }

        public void Analysis(List<byte> data)
        {
            var bits = new BitListEnumerator(data.ToList());
            //packet id
            bits.MoveNextToShort();
            var x = bits.MoveNextToInt();
            var y = bits.MoveNextToInt();
            var chunkPos = new Vector2Int(x, y);

            var chunkBlocks = new int[ChunkConstant.ChunkSize,ChunkConstant.ChunkSize];
            var blockDirections = new BlockDirection[ChunkConstant.ChunkSize,ChunkConstant.ChunkSize];
            
            //analysis block data
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    chunkBlocks[i, j] = GetBlockId(bits);
                    blockDirections[i, j] = GetBlockDirection(bits);
                }
            }
            
            var mapTiles = new int[ChunkConstant.ChunkSize,ChunkConstant.ChunkSize];
            //analysis map data
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    mapTiles[i, j] = GetBlockId(bits);
                }
            }
            
            //chunk data event
            _networkReceivedChunkDataEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(chunkPos, chunkBlocks,blockDirections,mapTiles));
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
        
        private BlockDirection GetBlockDirection(BitListEnumerator bit)
        {
            var bit1 = bit.MoveNextToBit();
            var bit2 = bit.MoveNextToBit();
            
            if (!bit1 && !bit2)
            {
                return BlockDirection.North;
            }
            
            if (!bit1 && bit2)
            {
                return BlockDirection.East;
            }
            
            if (bit1 && !bit2)
            {
                return BlockDirection.South;
            }
            else
            {
                return BlockDirection.West;
            }
            
        }
    }
}