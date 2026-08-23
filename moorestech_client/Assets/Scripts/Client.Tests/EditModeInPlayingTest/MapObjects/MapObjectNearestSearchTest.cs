using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEditor;
using UnityEngine;
using VContainer;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.MapObjects
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// 実サーバーと繋いだ状態で最寄りmapObject探索の索引が破壊と候補集合に追従することを検証する。
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies the nearest map-object index follows destruction and candidate sets against the real server.
    /// </summary>
    public class MapObjectNearestSearchTest
    {
        [UnityTest]
        public IEnumerator DestroyedMapObjectDropsOutOfNearestSearchAfterServerBroadcast()
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
                var datastore = await LoadDatastore();

                var layouts = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().MapLayout.MapObjects;
                Assert.IsNotEmpty(layouts, "test world has no map objects");

                var target = layouts[0];
                var candidateGuids = new HashSet<Guid> { new(target.MapObjectGuid) };
                var position = new Vector3(target.X, target.Y, target.Z);

                var beforeDestroy = datastore.SearchNearestMapObject(candidateGuids, position);
                Assert.IsNotNull(beforeDestroy, $"map object {target.InstanceId} was not instantiated");
                Assert.AreEqual(target.InstanceId, beforeDestroy.InstanceId, "resolved a different instance than the one being destroyed");

                // 実サーバーで破壊し、実配信経路(OnDestroyMapObject→イベントパケット→OnUpdateMapObjectのMarkDirty)を通す
                // Destroy on the real server so the actual broadcast path (OnDestroyMapObject → event packet → OnUpdateMapObject's MarkDirty) is exercised
                ServerContext.MapObjectDatastore.Get(target.InstanceId).Destroy();

                await UniTask.WaitUntil(() => datastore.SearchNearestMapObject(candidateGuids, position) != beforeDestroy)
                    .Timeout(TimeSpan.FromSeconds(10));

                Assert.AreNotSame(beforeDestroy, datastore.SearchNearestMapObject(candidateGuids, position),
                    "destroyed map object is still returned by SearchNearestMapObject after the server broadcast");
            }

            #endregion
        }

        // 候補GUIDが1件だけの経路はMapObjectRotationTestが既に通しているので、ここでは2件以上の候補集合から
        // 最も近い側が選ばれることをクエリ位置を反転させた双方向で確認する（earnItemピンの実経路）
        // The single-candidate path is already covered by MapObjectRotationTest, so here two candidates are
        // passed and the nearer one is asserted in both directions by flipping the query position (the earnItem pin's real path)
        [UnityTest]
        public IEnumerator SearchNearestMapObjectPicksTheCloserOfMultipleCandidates()
        {
            EnterPlayModeUtil();

            yield return new EnterPlayMode(expectDomainReload: true);

            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                var datastore = await LoadDatastore();

                var (nearLayout, farLayout) = FindTwoDistinctGuidLayouts();
                var candidateGuids = new HashSet<Guid> { new(nearLayout.MapObjectGuid), new(farLayout.MapObjectGuid) };

                var nearResult = datastore.SearchNearestMapObject(candidateGuids, new Vector3(nearLayout.X, nearLayout.Y, nearLayout.Z));
                Assert.IsNotNull(nearResult, "expected a candidate to be found near the near layout's position");
                Assert.AreEqual(
                    new Guid(nearLayout.MapObjectGuid), nearResult.MapObjectGuid,
                    "with two candidates in the set, the nearer one must win");

                var farResult = datastore.SearchNearestMapObject(candidateGuids, new Vector3(farLayout.X, farLayout.Y, farLayout.Z));
                Assert.IsNotNull(farResult, "expected a candidate to be found near the far layout's position");
                Assert.AreEqual(
                    new Guid(farLayout.MapObjectGuid), farResult.MapObjectGuid,
                    "flipping the query position must flip which candidate wins, proving the winner is not a fixed candidate");
            }

            // 位置の異なる2つのGUID種を選び出す。同一位置に複数種を置く前例は無いため異GUID2件で十分成立する
            // Pick two guid types at distinct positions; no fixture places two types at the same spot, so distinct guids suffice
            (MapObjectLayoutMessagePack near, MapObjectLayoutMessagePack far) FindTwoDistinctGuidLayouts()
            {
                var layouts = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().MapLayout.MapObjects;

                var first = layouts[0];
                foreach (var layout in layouts)
                {
                    if (layout.MapObjectGuid == first.MapObjectGuid) continue;
                    return (first, layout);
                }

                Assert.Fail("test world has fewer than two distinct map object guids");
                return (null, null);
            }

            #endregion
        }

        // 生成完了まで待ってからdatastoreを返す。索引の検証は全個体が登録済みであることが前提
        // Return the datastore only after instantiation finishes; index assertions assume every object is registered
        private static async UniTask<MapObjectGameObjectDatastore> LoadDatastore()
        {
            await LoadMainGame();

            var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>(FindObjectsInactive.Include);
            Assert.IsNotNull(datastore, "MapObjectGameObjectDatastore was not found in scene");

            await datastore.WaitForInitialApplyAsync();
            return datastore;
        }
    }
}
