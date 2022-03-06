using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGame.Basic;
using MainGame.GameLogic.Chunk;
using MainGame.Network.Event;
using MainGame.Network.Receive.EventPacket;
using MainGame.UnityView.Chunk;
using NUnit.Framework;
using UnityEngine;

namespace Test.EditModeTest.GameLogic
{
    public class ChunkDataStoreTest
    {
        private readonly BlockDirection[,] _emptyBlockDirections =
            new BlockDirection[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
        
        //正しくチャンクがセットできるかのテスト
        [Test]
        public void SetChunkTest()
        {
            var chunkEvent = new NetworkReceivedChunkDataEvent();
            var chunkGameObjects = new GameObject().AddComponent<ChunkBlockGameObjectDataStore>();
            var chunkDataStore = new ChunkDataStoreCache(chunkEvent,chunkGameObjects);
            
            //リフレクションでチャンクのデータを取得する
            var chunk = (Dictionary<Vector2Int, int[,]>)chunkDataStore.GetType().GetField("_chunk", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chunkDataStore);
            
            //チャンクをイベント経由でセットする
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(0,0), EmptyBlockArray(),_emptyBlockDirections));
            //チャンクが正しくセットできているかチェック
            Assert.True(chunk.ContainsKey(new Vector2Int(0,0)));
            
            
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(20,0), EmptyBlockArray(),_emptyBlockDirections));
            Assert.True(chunk.ContainsKey(new Vector2Int(20,0)));
            
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(20,-100), EmptyBlockArray(),_emptyBlockDirections));
            Assert.True(chunk.ContainsKey(new Vector2Int(20,-100)));
        }

        private int[,] EmptyBlockArray()
        {
            return new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
        }

    }
}