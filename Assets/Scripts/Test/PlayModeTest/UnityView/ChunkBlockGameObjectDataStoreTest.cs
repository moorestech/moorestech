using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.Constant;
using MainGame.GameLogic.Chunk;
using MainGame.UnityView.Chunk;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Test.PlayModeTest.UnityView
{
    public class ChunkBlockGameObjectDataStoreTest
    {
        [Test]
        public void PlaceAndRemoveEventTest()
        {
            //初期設定
            var dataStore = new GameObject().AddComponent<ChunkBlockGameObjectDataStore>();
            var blockUpdateEvent = new BlockUpdateEvent();
            var blocksData = AssetDatabase.LoadAssetAtPath<BlockObjects>("Assets/ScriptableObject/BlockObjects.asset");
            dataStore.Construct(blockUpdateEvent,blocksData);
            
            
            
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
            blockUpdateEvent.DiffChunkUpdate(chunkPosition,ids);
            
            
            
            //ブロックが置かれているか確認する
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(4,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-20, 0, 20)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-18, 0, 20)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-10, 0, 30)));
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-7, 0, 24)));
            
            
            
            
            //ブロックがもう一個増えた時のテスト
            var newIds = new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
            Array.Copy(ids,newIds,0);
            newIds[5, 5] = 1;
            //イベントを発火
            blockUpdateEvent.DiffChunkUpdate(chunkPosition,newIds,ids);
            Assert.AreEqual(5,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(-20, 0, 20)));
            
            
            //何もないチャンクが発火され、ブロックがなくなるテスト
            blockUpdateEvent.DiffChunkUpdate(chunkPosition,new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize],ids);
            blocks = GetBlocks(dataStore.transform);
            Assert.AreEqual(0,blocks.Count);
            
            
            
            //一つのブロックの設置
            blockUpdateEvent.OnBlockUpdate(new Vector2Int(0,0),1);
            //チェック
            Assert.AreEqual(6,blocks.Count);
            Assert.True(blocks.Any(block => block.transform.position == new Vector3(0, 0, 0)));
            
            
            
            //一つのブロックの削除
            blockUpdateEvent.OnBlockUpdate(new Vector2Int(-20,20),BlockConstant.NullBlockId);
            Assert.AreEqual(5,blocks.Count);
            Assert.False(blocks.Any(block => block.transform.position == new Vector3(-20, 0,20)));
            
            
            
            
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