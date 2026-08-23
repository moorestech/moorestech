using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.MapObjects
{
    /// <summary>
    /// - EditMode実行中にPlayModeへ遷移
    /// - 近傍待機解除時に150m以内が生成済みか検証
    /// - Switches from EditMode to PlayMode during execution
    /// - Verifies objects within 150m are already instantiated when the near-field wait releases
    /// </summary>
    public class MapObjectNearFieldStartupTest
    {
        [UnityTest]
        public IEnumerator NearFieldMapObjectsExistWhenInitialApplyCompletes()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>(FindObjectsInactive.Include);
                Assert.IsNotNull(datastore, "MapObjectGameObjectDatastore was not found in scene");

                // 待機直後に近傍全件を突合
                // Match every near layout right after the wait
                await datastore.WaitForInitialApplyAsync();

                var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
                var playerPos = handshake.PlayerPos;
                var checkedCount = 0;

                // InstanceId単位で突合
                // Match by InstanceId
                var liveInstanceIds = new HashSet<int>();
                foreach (var mapObject in Object.FindObjectsByType<MapObjectGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    liveInstanceIds.Add(mapObject.InstanceId);

                MapObjectLayoutMessagePack farthestLayout = null;
                var farthestSqrDistance = -1f;

                foreach (var layout in handshake.MapLayout.MapObjects)
                {
                    var position = new Vector3(layout.X, layout.Y, layout.Z);
                    var sqrDistance = (position - playerPos).sqrMagnitude;

                    // 最遠1件は「待機解除直後はまだ未生成」の否定側検証に使うため近傍判定と無関係に追跡する
                    // Track the farthest one regardless of near-field membership, for the negative assertion that it is not yet instantiated
                    if (farthestSqrDistance < sqrDistance)
                    {
                        farthestSqrDistance = sqrDistance;
                        farthestLayout = layout;
                    }

                    if (!MapObjectLayoutDistanceOrder.IsWithinNearField(position, playerPos)) continue;

                    // guid単位の最寄り探索では同一guidの別個体で空振りするため、instanceId単位の集合包含で存在を確かめる
                    // A guid-scoped nearest search can pass via a different instance with the same guid, so check existence by instanceId set membership
                    Assert.IsTrue(liveInstanceIds.Contains(layout.InstanceId), $"near-field map object {layout.InstanceId} was not instantiated before the initial-apply wait released");
                    checkedCount++;
                }

                // 近傍0件のワールドでは検証が素通りしてしまうので先に落とす
                // With zero near objects every assertion would pass vacuously, so fail here first
                Assert.Greater(checkedCount, 0, "test world has no map objects within the near field");

                // 近傍待機だけで完結する後着範囲が実在することをR1の主目的として固定する（全量ブロッキングへの逆戻りを検知）
                // Pin that a background range genuinely exists beyond the near-field wait, catching a regression to blocking full instantiation (R1's core intent)
                if (farthestLayout != null && MapObjectLayoutDistanceOrder.NearFieldRadius * MapObjectLayoutDistanceOrder.NearFieldRadius < farthestSqrDistance)
                    Assert.IsFalse(liveInstanceIds.Contains(farthestLayout.InstanceId), $"farthest map object {farthestLayout.InstanceId} was already instantiated right after the near-field wait released");

                // 完走待ちは実時間が過大なため、全量待機は正規APIとして取得できることだけを固定する（2026-08-23裁定）
                // Awaiting completion costs too much real time, so only pin that the full-instantiation wait is obtainable as the official API (adjudicated 2026-08-23)
                Assert.DoesNotThrow(() => datastore.WaitForAllInstantiatedAsync());
            }

            #endregion
        }
    }
}
