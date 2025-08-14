using System.Collections;
using Client.Game.InGame.Entity;
using Client.Game.InGame.UI.Challenge;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.PlayModeTest.Util.PlayModeTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.PlayModeTest
{
    public class BeltConveyorItemObjectTest
    {
        /// <summary>
        /// アイテムエンティティのオブジェクトが特定の範囲内に常にいることをチェックする
        /// </summary>
        [UnityTest]
        public IEnumerator BeltConveyorItemEntityPositionTest()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return SetUp().ToCoroutine();
            
            yield return AssertTest().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask SetUp()
            {
                await LoadMainGame();
                
                // TODO このブロックの名前を英語にする
                // TODO Change the name of this block to English
                var chest = PlaceBlock("量子チェスト", Vector3Int.zero, BlockDirection.North);
                PlaceBlock("ベルトコンベア", new Vector3Int(0, 0, 1), BlockDirection.North);
                PlaceBlock("ベルトコンベア", new Vector3Int(0, 0, 2), BlockDirection.East);
                PlaceBlock("ベルトコンベア", new Vector3Int(1, 0, 2), BlockDirection.East);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 2), BlockDirection.South);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 1), BlockDirection.South);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 0), BlockDirection.South);
                
                // チェストにアイテムを入れる
                InsertItemToBlock(chest, new ItemId(1), 100);
            }
            
            async UniTask AssertTest()
            {
                // アイテムが常にベルトコンベアが置いてある範囲内にあるかどうかをチェックするためのバウディングボックス
                // Create a bounding box to check if the item is always within the range of the conveyor belt
                var itemEntityBoundingBox = new Bounds(Vector3.zero, new Vector3(2.9f, 1f, 2.9f));
                
                var entityDatastore = Object.FindObjectOfType<EntityObjectDatastore>();
                var startTime = Time.time;
                var testDuration = 10f;
                while (true)
                {
                    // 秒数経過したら終了
                    // Exit after a certain number of seconds
                    if (Time.time - startTime > testDuration) break;
                    
                    // アイテムエンティティをチェック
                    // Check the item entity
                    for (int i = 0; i < entityDatastore.transform.childCount; i++)
                    {
                        var itemEntity = entityDatastore.transform.GetChild(i).GetComponent<ItemEntityObject>();
                        if (itemEntity == null) continue;
                        
                        // アイテムエンティティの位置がベルトコンベアの範囲内にあるかどうかをチェック
                        // Check if the item entity's position is within the range of the conveyor belt
                        Assert.IsTrue(itemEntityBoundingBox.Contains(itemEntity.transform.position), $"Item entity {itemEntity.name} is out of bounds: {itemEntity.transform.position}");
                    }
                }
            }
            
            #endregion
        }
    }
}