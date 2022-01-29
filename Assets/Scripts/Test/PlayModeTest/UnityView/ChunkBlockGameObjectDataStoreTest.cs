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
            
            //チャンクの情報を作成
            var ids = new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
            ids[0, 0] = 1;
            var chunkPosition = new Vector2Int(0,0);
            //イベントを発火
            blockUpdateEvent.DiffChunkUpdate(chunkPosition,ids);
            
            
            //ブロックが置かれているか確認する
            var blocks = dataStore.transform.GetComponentsInChildren<Transform>();
            Assert.AreEqual(1,blocks.Length);
            Assert.AreEqual(new Vector3(0,0,0),blocks[0].position);

        }
    }
}