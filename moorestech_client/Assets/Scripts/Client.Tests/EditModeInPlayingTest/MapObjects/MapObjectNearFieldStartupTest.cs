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
using UniRx;
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
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientNearFieldStartup")]
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
                await datastore.IsNearFieldInstantiated.Where(static completed => completed).First().ToUniTask();

                var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
                var nearFieldOrder = MapObjectLayoutDistanceOrder.SortNearFieldFirst(
                    handshake.MapLayout.MapObjects, handshake.PlayerPos);

                // InstanceId単位で突合
                // Match by InstanceId
                var liveInstanceIds = new HashSet<int>();
                foreach (var mapObject in Object.FindObjectsByType<MapObjectGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    liveInstanceIds.Add(mapObject.InstanceId);

                for (var index = 0; index < nearFieldOrder.NearFieldCount; index++)
                {
                    var layout = nearFieldOrder.Entries[index].Layout;

                    // guid単位の最寄り探索では同一guidの別個体で空振りするため、instanceId単位の集合包含で存在を確かめる
                    // A guid-scoped nearest search can pass via a different instance with the same guid, so check existence by instanceId set membership
                    Assert.IsTrue(liveInstanceIds.Contains(layout.InstanceId), $"near-field map object {layout.InstanceId} was not instantiated before the initial-apply wait released");
                }

                // 近傍0件のワールドでは検証が素通りしてしまうので先に落とす
                // With zero near objects every assertion would pass vacuously, so fail here first
                Assert.Greater(nearFieldOrder.NearFieldCount, 0, "test world has no map objects within the near field");

            }

            #endregion
        }
    }
}
