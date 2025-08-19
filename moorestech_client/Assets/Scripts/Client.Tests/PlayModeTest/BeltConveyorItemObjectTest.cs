using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
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
            
            yield return AssertTest("Normal Test").ToCoroutine();
            
            // ブロックを削除してもアイテムが範囲内にあるかをチェックする
            // Check if the item is still within the range after removing the blocks
            RemoveBlock(new Vector3Int(0, 0, 2));
            
            yield return AssertTest("Removed Test").ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask SetUp()
            {
                await LoadMainGame();
                
                // TODO このブロックの名前を英語にする
                // TODO Change the name of this block to English
                var chest = PlaceBlock("量子チェスト", Vector3Int.zero, BlockDirection.North);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(0, 0, 1), BlockDirection.North);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(0, 0, 2), BlockDirection.East);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(1, 0, 2), BlockDirection.East);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(2, 0, 2), BlockDirection.South);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(2, 0, 1), BlockDirection.South);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(2, 0, 0), BlockDirection.West);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(1, 0, 0), BlockDirection.West);
                
                // チェストにアイテムを入れる
                InsertItemToBlock(chest, new ItemId(1), 100);
                
                // 物理演算の同期を待つ
                Physics.SyncTransforms();
                await UniTask.WaitForFixedUpdate();
            }
            
            async UniTask AssertTest(string testPhase)
            {
                // アイテムが常にベルトコンベアが置いてある範囲内にあるかどうかをチェックするためのバウディングボックス
                // Create a bounding box to check if the item is always within the range of the conveyor belt
                var itemEntityBoundingBox = new Bounds(new Vector3(1.5f, 0.5f, 1.5f), new Vector3(2.9f, 1f, 2.9f));
                
                var entityDatastore = Object.FindObjectOfType<EntityObjectDatastore>();
                var startTime = Time.time;
                var testDuration = 3;
                
                var itemObjects = new Dictionary<long,Transform>();
                var intervalCheckTime = new Dictionary<long,DateTime>();
                
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
                        Assert.IsTrue(itemEntityBoundingBox.Contains(itemEntity.transform.position), $"{testPhase} : Item entity {itemEntity.name} is out of bounds: {itemEntity.transform.position}");

                        
                        // アイテムエンティティの生成時間を記録
                        // Record the creation time of the item entity
                        var entityId = itemEntity.EntityId;
                        if (!intervalCheckTime.ContainsKey(entityId))
                        {
                            intervalCheckTime[entityId] = DateTime.Now;
                        }
                        
                        // 長い時間をかけてでもアイテムが移動していることをチェック
                        // Check if the item has moved
                        var nowTransform = itemEntity.transform;
                        var oldTransform = itemObjects.GetValueOrDefault(entityId);
                        var instantiateTime = intervalCheckTime.GetValueOrDefault(entityId);
                        if (nowTransform && oldTransform && 1f < (DateTime.Now - instantiateTime).TotalSeconds)
                        {
                            const float itemDistance = 0.1f;
                            Assert.GreaterOrEqual(
                                Vector3.Distance(nowTransform.position, oldTransform.position),
                                itemDistance,
                                $" EntityId:{entityId}, {testPhase} : Item entity {itemEntity.name} did not move enough: {Vector3.Distance(nowTransform.position, oldTransform.position)}"
                            );
                            // アイテムエンティティの位置を記録
                            // Record the position of the item entity
                            itemObjects[entityId] = nowTransform;
                            intervalCheckTime[entityId] = DateTime.Now;
                        }
                    }
                    
                    // 物理演算の同期を確実にする
                    Physics.SyncTransforms();
                    await UniTask.WaitForFixedUpdate();
                }
            }
            
            #endregion
        }
    }
}