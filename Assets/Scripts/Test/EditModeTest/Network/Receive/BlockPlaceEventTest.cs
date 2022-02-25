using System.Collections.Generic;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Receive.EventPacket;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest.Network.Receive
{
    public class BlockPlaceEventTest
    {
        /// <summary>
        /// BlockPlaceEvent単体のテスト
        /// </summary>
        [Test]
        public void BlockPlaceEventToSetDataStoreTest()
        {
            //テスト用のBlockPlaceEventを生成
            var chunkUpdateEvent = new NetworkReceivedChunkDataEvent();
            var blockPlaceEvent = new BlockPlaceEvent(chunkUpdateEvent);
            
            //イベントをサブスクライブする
            var dataStore = new TestChunkDataStore();
            chunkUpdateEvent.Subscribe(dataStore.OnUpdateChunk,dataStore.OnUpdateBlock);

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
            //テスト用のBlockPlaceEventを生成
            var chunkUpdateEvent = new NetworkReceivedChunkDataEvent();
            var packetAnalysis = new AllReceivePacketAnalysisService(chunkUpdateEvent,new MainInventoryUpdateEvent(),new CraftingInventoryUpdateEvent());
            
            var blockPosition = new Vector2Int(10,20);
            var blockId = 3;
            var chunkPosition = new Vector2Int(0, 20);
            
            
            //イベントをサブスクライブする
            var dataStore = new TestChunkDataStore();
            chunkUpdateEvent.Subscribe(dataStore.OnUpdateChunk,dataStore.OnUpdateBlock);
            
            
            
            
            
            
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