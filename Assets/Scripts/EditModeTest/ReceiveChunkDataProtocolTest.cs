using System.Collections.Generic;
using MainGame.GameLogic.Interface;
using MainGame.Network.Receive;
using NUnit.Framework;
using UnityEngine;

namespace EditModeTest
{
    public class ReceiveChunkDataProtocolTest
    {
        [Test]
        public void ReceiveChunkDataProtocolToRegisterDataStoreTest()
        {
            var protocol = new ReceiveChunkDataProtocol(new TestDataStore());
            
        }
    }

    class TestDataStore : IChunkDataStore
    {
        public Dictionary<Vector2,int[,]> Data { get; set; }
        public void SetChunk(Vector2 chunkPosition, int[,] ids)
        {
            Data.Add(chunkPosition, ids);
        }
    }
}