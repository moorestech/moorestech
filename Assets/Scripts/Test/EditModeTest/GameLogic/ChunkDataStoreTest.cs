using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGame.Basic;
using MainGame.GameLogic.Chunk;
using MainGame.Network.Event;
using MainGame.Network.Receive.EventPacket;
using NUnit.Framework;
using UnityEngine;

namespace Test.EditModeTest.GameLogic
{
    public class ChunkDataStoreTest
    {
        //正しくチャンクがセットできるかのテスト
        [Test]
        public void SetChunkTest()
        {
            var chunkEvent = new ChunkUpdateEvent();
            var chunkDataStore = new ChunkDataStoreCache(chunkEvent);
            
            //リフレクションでチャンクのデータを取得する
            var chunk = (Dictionary<Vector2Int, int[,]>)chunkDataStore.GetType().GetField("_chunk", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chunkDataStore);
            
            //チャンクをイベント経由でセットする
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(0,0), EmptyBlockArray()));
            //チャンクが正しくセットできているかチェック
            Assert.True(chunk.ContainsKey(new Vector2Int(0,0)));
            
            
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(20,0), EmptyBlockArray()));
            Assert.True(chunk.ContainsKey(new Vector2Int(20,0)));
            
            chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                new Vector2Int(20,-100), EmptyBlockArray()));
            Assert.True(chunk.ContainsKey(new Vector2Int(20,-100)));
        }


        [Test]
        public void SetBlockTest()
        {
            var chunkEvent = new ChunkUpdateEvent();
            var chunkDataStore = new ChunkDataStoreCache(chunkEvent);

            //検証するチャンク
            var setChunks = new List<Vector2Int>()
            {
                new Vector2Int(40,40),
                new Vector2Int(-40,40),
                new Vector2Int(40,-40),
                new Vector2Int(-40,-40)
            };
            //検証するブロック
            var blocks = new Dictionary<(int, int), int>();
            blocks.Add((0,0), 1);
            blocks.Add((1,0), 2);
            blocks.Add((1,1), 3);
            blocks.Add((18,18), 4);
            blocks.Add((18,19), 5);
            blocks.Add((19,18), 6);
            blocks.Add((19,19), 6);
            
            
            //検証用チャンクをセットする
            setChunks.ForEach(c => 
                chunkEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(c, EmptyBlockArray())));
                
            
            
            //イベント経由でブロックをセットする
            setChunks.ForEach(c =>
            {
                foreach (var block in blocks)
                {
                    var x = c.x + block.Key.Item1;
                    var y = c.y + block.Key.Item2;
                    chunkEvent.InvokeBlockUpdateEvent(new OnBlockUpdateEventProperties(
                        new Vector2Int( x,y),
                        block.Value));   
                }
            });
            
            //リフレクションでチャンクのデータを取得する
            var chunk = (Dictionary<Vector2Int, int[,]>)chunkDataStore.GetType().GetField("_chunk", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chunkDataStore);
            
            //ブロックが正しくセットできているかテストする
            setChunks.ForEach(c =>
            {
                foreach (var block in blocks)
                {
                    var i = block.Key.Item1;
                    var j = block.Key.Item2;
                    var cBlock = chunk[c];
                    Assert.AreEqual(block.Value, cBlock[i,j]);
                }
            });
        }
        

        private int[,] EmptyBlockArray()
        {
            return new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
        }

    }
}