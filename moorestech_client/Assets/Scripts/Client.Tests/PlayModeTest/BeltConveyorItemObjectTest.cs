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
            
            // 全てのベルトコンベアが設置されている座標
            // All conveyor positions where the conveyor is installed
            var allConveyorPositions = new List<Vector3Int>
            {
                new(0, 0, 1),
                new(0, 0, 2),
            };
            
            yield return AssertTest(allConveyorPositions).ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask SetUp()
            {
                await LoadMainGame();
                
                // TODO このブロックの名前を英語にする
                // TODO Change the name of this block to English
                var chest = PlaceBlock("量子チェスト", Vector3Int.zero, BlockDirection.North);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(0, 0, 1), BlockDirection.North);
                PlaceBlock("直進高速ベルトコンベア", new Vector3Int(0, 0, 2), BlockDirection.North);
                
                // チェストにアイテムを入れる
                InsertItemToBlock(chest, new ItemId(1), 100);
                
                // 物理演算の同期を待つ
                Physics.SyncTransforms();
                await UniTask.WaitForFixedUpdate();
            }
            
            async UniTask AssertTest(List<Vector3Int> allowedPositions)
            {
                var entityDatastore = Object.FindObjectOfType<EntityObjectDatastore>();
                var startTime = Time.time;
                var testDuration = 10;
                
                var itemObjects = new Dictionary<long,Vector3>();
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
                        
                        // アイテムが許可された座標の近くにのみ存在することをチェック
                        // Check that items only exist near allowed positions
                        AssertItemOnlyAtAllowedPositions(itemEntity, allowedPositions);
                        
                        // アイテムが移動していることをチェック
                        // Check that the item is moving
                        AssertMoveItem(itemEntity, itemObjects, intervalCheckTime);
                    }
                    
                    // 物理演算の同期を確実にする
                    Physics.SyncTransforms();
                    await UniTask.WaitForFixedUpdate();
                }
            }
            
            void AssertItemOnlyAtAllowedPositions(ItemEntityObject itemEntity, List<Vector3Int> allowedPositions)
            {
                const float tolerance = 0.1f; // 誤差
                bool isInAllowedArea = false;
                Vector3Int closestPosition = Vector3Int.zero;
                float minDistance = float.MaxValue;
                
                foreach (var allowedPos in allowedPositions)
                {
                    // 各座標から(1, 1, 1)の範囲 + 誤差0.1のバウンディングボックスを作成
                    // Create bounding box from position to position + (1,1,1) with 0.1 tolerance
                    var min = new Vector3(allowedPos.x - tolerance, allowedPos.y - tolerance, allowedPos.z - tolerance);
                    var max = new Vector3(allowedPos.x + 1 + tolerance, allowedPos.y + 1 + tolerance, allowedPos.z + 1 + tolerance);
                    var bounds = new Bounds();
                    bounds.SetMinMax(min, max);
                    
                    if (bounds.Contains(itemEntity.transform.position))
                    {
                        isInAllowedArea = true;
                        break;
                    }
                    
                    // 最も近い位置を記録（デバッグ用）
                    var center = new Vector3(allowedPos.x + 0.5f, allowedPos.y + 0.5f, allowedPos.z + 0.5f);
                    var distance = Vector3.Distance(itemEntity.transform.position, center);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPosition = allowedPos;
                    }
                }
                
                Assert.IsTrue(isInAllowedArea, 
                    $"Item entity {itemEntity.name} at {itemEntity.transform.position} is not in any allowed area. " +
                    $"Closest allowed position is {closestPosition} with distance {minDistance:F2}. " +
                    $"Allowed positions: [{string.Join(", ", allowedPositions)}]");
            }
            
            void AssertMoveItem(ItemEntityObject itemEntity, Dictionary<long, Vector3> itemObjects, Dictionary<long, DateTime> intervalCheckTime)
            {
                // アイテムエンティティの生成時間を記録
                // Record the creation time of the item entity
                var entityId = itemEntity.EntityId;
                if (!intervalCheckTime.ContainsKey(entityId))
                {
                    intervalCheckTime[entityId] = DateTime.Now;
                    itemObjects[entityId] = itemEntity.transform.position;
                }
                
                // 長い時間をかけてでもアイテムが移動していることをチェック
                // Check if the item has moved
                var instantiateTime = intervalCheckTime.GetValueOrDefault(entityId);
                if (1f < (DateTime.Now - instantiateTime).TotalSeconds)
                {
                    var nowPosition = itemEntity.transform.position;
                    var distance = Vector3.Distance(nowPosition, itemObjects.GetValueOrDefault(entityId));
                    const float itemDistance = 0.1f;
                    Assert.GreaterOrEqual(
                        distance,
                        itemDistance,
                        $" EntityId:{entityId} : Item entity {itemEntity.name} did not move enough: {distance}"
                    );
                    // アイテムエンティティの位置を更新
                    // Update the position of the item entity
                    itemObjects[entityId] = nowPosition;
                    intervalCheckTime[entityId] = DateTime.Now;
                }
            }
            
            #endregion
        }
    }
}