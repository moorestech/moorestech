using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGame.Basic;
using MainGame.GameLogic.Chunk;
using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Test.PlayModeTest.UnityView
{
    public class ChunkBlockGameObjectDataStoreTest
    {
        private readonly BlockDirection[,] _emptyBlockDirections =
            new BlockDirection[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
        
        
        [UnityTest]
        public IEnumerator PlaceAndRemoveEventTest()
        {
            //初期設定
            var gameObject = new GameObject();
            gameObject.AddComponent<MainThreadExecutionQueue>();
            var dataStore = gameObject.AddComponent<ChunkBlockGameObjectDataStore>();
            var chunkReceivedEvent = new NetworkReceivedChunkDataEvent();
            new ChunkDataStoreCache(chunkReceivedEvent,dataStore);
            var blocksData = AssetDatabase.LoadAssetAtPath<BlockObjects>("Assets/ScriptableObject/BlockObjects.asset");
            dataStore.Construct(blocksData);
            
            
            
            //ブロックが置かれていないことを確認する
            var blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(0,blocks.Count);
            
            
            
            //チャンクの情報を作成
            var ids = new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
            ids[0, 0] = 1;
            ids[2, 0] = 1;
            ids[10, 10] = 1;
            ids[13, 4] = 1;
            var chunkPosition = new Vector2Int(-20,20);
            //イベントを発火
            chunkReceivedEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(chunkPosition,ids,_emptyBlockDirections));
            
            
            //Instansiateのために1フレーム待機
            yield return null;
            
            //ブロックが置かれているか確認する
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(4,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-20, 0, 20)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-18, 0, 20)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-10, 0, 30)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-7, 0, 24)));
            
            
            
            
            //ブロックがもう一個増えた時のテスト
            var newIds = new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
            newIds[0, 0] = 1;
            newIds[2, 0] = 1;
            newIds[10, 10] = 1;
            newIds[13, 4] = 1;
            newIds[5, 5] = 1;
            //イベントを発火
            chunkReceivedEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(chunkPosition,newIds,_emptyBlockDirections));
            
            //Instansiateのために1フレーム待機
            yield return null;
            
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(5,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-15, 0, 25)));
            
            
            //何もないチャンクが発火され、ブロックがなくなるテスト
            chunkReceivedEvent.InvokeChunkUpdateEvent(new OnChunkUpdateEventProperties(
                chunkPosition,new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize],_emptyBlockDirections));
            
            //Destoryのために1フレーム待機
            yield return null;
            
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(0,blocks.Count);
            
            
            
            //一つのブロックの設置
            chunkReceivedEvent.InvokeBlockUpdateEvent(
                new OnBlockUpdateEventProperties(chunkPosition,1,BlockDirection.North));
            
            //Instansiateのために1フレーム待機
            yield return null;
            
            //チェック
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(1,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(chunkPosition.x, 0, chunkPosition.y)));
            
            
            
            //一つのブロックの削除
            chunkReceivedEvent.InvokeBlockUpdateEvent(
                new OnBlockUpdateEventProperties(chunkPosition,BlockConstant.NullBlockId,BlockDirection.North));
            
            //Destoryのために1フレーム待機
            yield return null;
            
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(0,blocks.Count);
        }

        private List<Transform> GetBlocks(Transform dataStore)
        {
            var blocks = dataStore.transform.GetComponentsInChildren<Transform>().ToList();
            //データストアの一個したの階層のオブジェクトのみを取得
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                if (blocks[i].parent == dataStore.transform) continue;
                blocks.RemoveAt(i);
            }

            return blocks;
        }
    }
}