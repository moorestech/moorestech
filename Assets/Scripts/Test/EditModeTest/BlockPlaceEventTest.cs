using System.Collections.Generic;
using MainGame.Network;
using MainGame.Network.Receive.Event;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest
{
    public class BlockPlaceEventTest
    {
        /// <summary>
        /// BlockPlaceEvent単体のテスト
        /// </summary>
        [Test]
        public void BlockPlaceEventToSetDataStoreTest()
        {
            var dataStore = new TestDataStore();
            var blockPlaceEvent = new BlockPlaceEvent(dataStore);

            var blockPosition = new Vector2Int(10,20);
            var blockId = 3;
            var chunkPosition = new Vector2Int(0, 20);
            
            //パケットの解析
            blockPlaceEvent.Analysis(GetBlockPlaceEventData(blockPosition,blockId));
            
            //チャンクが新しく作られているか
            Assert.True(dataStore.Data.ContainsKey(chunkPosition));
            //チャンクにブロックが追加されているか
            Assert.AreEqual(blockId,dataStore.Data[chunkPosition][10,0]);
        }

        /// <summary>
        /// AllReceivePacketAnalysisService経由のブロック設置イベントのテスト
        /// </summary>
        [Test]
        public void BlockPlaceEventViaAllReceivePacketAnalysisServiceTest()
        {
            var dataStore = new TestDataStore();
            var packetAnalysis = new AllReceivePacketAnalysisService(dataStore);
            
            var blockPosition = new Vector2Int(10,20);
            var blockId = 3;
            var chunkPosition = new Vector2Int(0, 20);
            
            //パケットの解析
            packetAnalysis.Analysis(GetBlockPlaceEventData(blockPosition,blockId).ToArray());
            
            //チャンクが新しく作られているか
            Assert.True(dataStore.Data.ContainsKey(chunkPosition));
            //チャンクにブロックが追加されているか
            Assert.AreEqual(blockId,dataStore.Data[chunkPosition][10,0]);
        }
        
        List<byte> GetBlockPlaceEventData(Vector2Int blockPosition, int blockId)
        {
            var data = new List<byte>();
            data.AddRange(ToByteList.Convert((short)3));
            data.AddRange(ToByteList.Convert((short)0));
            data.AddRange(ToByteList.Convert(blockPosition.x));
            data.AddRange(ToByteList.Convert(blockPosition.y));
            data.AddRange(ToByteList.Convert(blockId));
            return data;
        }
    }
}