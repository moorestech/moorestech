using System;
using System.Collections;
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
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// map.jsonに書かれた姿勢とスケールが実インスタンスへ届くことを実機検証する。
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies the rotation and scale written in map.json reach the real instance.
    /// </summary>
    public class MapObjectRotationTest
    {
        // 向きが一致しているとみなす許容角。float往復のぶんだけ緩めてある
        // The angle within which two facings count as equal, loosened only by the float round trip
        private const float RotationTolerance = 0.01f;

        // スケールが一致しているとみなす許容差。float往復のぶんだけ緩めてある
        // The difference within which two scales count as equal, loosened only by the float round trip
        private const float ScaleTolerance = 0.001f;

        [UnityTest]
        public IEnumerator MapObjectsMatchTheFacingAndScaleTheLayoutCarries()
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

            // IsNearFieldがローカル関数として同階層に並ぶため、Bodyで解決した値をここで共有する
            // Shared here so IsNearField, a sibling local function, can see the value resolved inside Body
            Vector3 playerPos = default;

            async UniTask Body()
            {
                await LoadMainGame();

                var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>(FindObjectsInactive.Include);
                Assert.IsNotNull(datastore, "MapObjectGameObjectDatastore was not found in scene");

                // 全量生成の完了待ちは実時間が過大なため、近傍待機で完結させ突き合わせ先も近傍から選ぶ（2026-08-23裁定）
                // Awaiting full instantiation costs too much real time, so stop at the near-field gate and match near objects only (adjudicated 2026-08-23)
                await datastore.WaitForInitialApplyAsync();

                // playerPosはループ内で毎回DI解決すると79,000件規模でコストが跳ねるため、ここで1回だけ解決してIsNearFieldへ渡す
                // Resolving playerPos on every loop iteration would spike cost at the 79,000 scale, so resolve it once here and pass it to IsNearField
                playerPos = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().PlayerPos;

                var turnedLayout = FindTurnedLayout();
                var expectedRotation = new Quaternion(
                    turnedLayout.RotationX, turnedLayout.RotationY, turnedLayout.RotationZ, turnedLayout.RotationW);
                var turnedInstance = SearchInstance(datastore, turnedLayout);

                Assert.Less(
                    Quaternion.Angle(expectedRotation, turnedInstance.transform.rotation), RotationTolerance,
                    "the instantiated map object does not face the direction the layout carries");

                var scaledLayout = FindScaledLayout();
                var expectedScale = new Vector3(scaledLayout.ScaleX, scaledLayout.ScaleY, scaledLayout.ScaleZ);
                var scaledInstance = SearchInstance(datastore, scaledLayout);

                Assert.Less(
                    Vector3.Distance(expectedScale, scaledInstance.transform.localScale), ScaleTolerance,
                    "the instantiated map object does not carry the scale the layout carries");
            }

            MapObjectGameObject SearchInstance(MapObjectGameObjectDatastore datastore, MapObjectLayoutMessagePack layout)
            {
                var instance = datastore.SearchNearestMapObject(
                    new Guid(layout.MapObjectGuid), new Vector3(layout.X, layout.Y, layout.Z));

                Assert.IsNotNull(instance, $"map object {layout.InstanceId} was not instantiated");
                return instance;
            }

            // 向きを持たない個体で突き合わせるとidentity固定の実装でも通ってしまう。回っている1件を選び出す
            // Matching against an unturned object would pass even with a hard-coded identity, so the turned one is picked out
            MapObjectLayoutMessagePack FindTurnedLayout()
            {
                var layouts = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().MapLayout.MapObjects;

                foreach (var layout in layouts)
                {
                    if (!IsNearField(layout)) continue;

                    var rotation = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
                    if (RotationTolerance < Quaternion.Angle(Quaternion.identity, rotation)) return layout;
                }

                Assert.Fail("test world has no turned map object within the near field");
                return null;
            }

            // 等倍の個体で突き合わせるとlocalScale適用を消しても通ってしまう。軸ごとに違う倍率の1件を選び出す
            // Matching against a unit-scaled object would pass even with the localScale assignment deleted, so the per-axis scaled one is picked out
            MapObjectLayoutMessagePack FindScaledLayout()
            {
                var layouts = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().MapLayout.MapObjects;

                foreach (var layout in layouts)
                {
                    if (!IsNearField(layout)) continue;
                    if (Mathf.Approximately(layout.ScaleX, layout.ScaleY) &&
                        Mathf.Approximately(layout.ScaleY, layout.ScaleZ)) continue;

                    return layout;
                }

                Assert.Fail("test world has no map object scaled differently per axis within the near field");
                return null;
            }

            // 後着個体はまだ生成されていないため、突き合わせ先は近傍レンジ内に限る
            // Background objects are not instantiated yet, so matching is limited to the near-field range
            bool IsNearField(MapObjectLayoutMessagePack layout)
            {
                var position = new Vector3(layout.X, layout.Y, layout.Z);
                return MapObjectLayoutDistanceOrder.IsWithinNearField(position, playerPos);
            }

            #endregion
        }
    }
}
