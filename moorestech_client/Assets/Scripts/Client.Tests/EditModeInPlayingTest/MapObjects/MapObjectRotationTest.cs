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
    /// map.jsonに書かれた姿勢が実インスタンスの向きへ届くことを実機検証する。
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies the rotation written in map.json reaches the real instance's facing.
    /// </summary>
    public class MapObjectRotationTest
    {
        // 向きが一致しているとみなす許容角。float往復のぶんだけ緩めてある
        // The angle within which two facings count as equal, loosened only by the float round trip
        private const float RotationTolerance = 0.01f;

        [UnityTest]
        public IEnumerator MapObjectsFaceTheDirectionTheLayoutCarries()
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

                // 初期化と同じawait経路を通し、生成が終わってから向きを見る
                // Use the same await path as startup so the facings are read after instantiation finishes
                await datastore.WaitForInitialApplyAsync();

                var layout = FindTurnedLayout();
                var expected = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
                var instance = datastore.SearchNearestMapObject(
                    new Guid(layout.MapObjectGuid), new Vector3(layout.X, layout.Y, layout.Z));

                Assert.IsNotNull(instance, $"map object {layout.InstanceId} was not instantiated");
                Assert.Less(
                    Quaternion.Angle(expected, instance.transform.rotation), RotationTolerance,
                    "the instantiated map object does not face the direction the layout carries");
            }

            // 向きを持たない個体で突き合わせるとidentity固定の実装でも通ってしまう。回っている1件を選び出す
            // Matching against an unturned object would pass even with a hard-coded identity, so the turned one is picked out
            MapObjectLayoutMessagePack FindTurnedLayout()
            {
                var layouts = ClientDIContext.DIContainer.DIContainerResolver
                    .Resolve<InitialHandshakeResponse>().MapLayout.MapObjects;

                foreach (var layout in layouts)
                {
                    var rotation = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
                    if (RotationTolerance < Quaternion.Angle(Quaternion.identity, rotation)) return layout;
                }

                Assert.Fail("test world has no turned map object");
                return null;
            }

            #endregion
        }
    }
}
