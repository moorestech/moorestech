using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
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
    /// 近傍待機の解除時点でPlayerPosから150m以内のmapObjectが全て生成済みであることを実機検証する。
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies every map object within 150m of PlayerPos already exists when the near-field wait releases.
    /// </summary>
    public class MapObjectNearFieldStartupTest
    {
        // datastore側のNearFieldRadius=150fと同じ値。定数公開はテスト専用publicになるため値を重ねる
        // Mirrors the datastore's NearFieldRadius=150f; exposing the constant would be a test-only public
        private const float NearFieldRadius = 150f;

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

                // 近傍待機（起動と同じ解除点）の直後に近傍layout全件の生存を突き合わせる
                // Right after the near-field wait (the same release point as startup), match every near layout
                await datastore.WaitForInitialApplyAsync();

                var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
                var playerPos = handshake.PlayerPos;
                var checkedCount = 0;

                foreach (var layout in handshake.MapLayout.MapObjects)
                {
                    var position = new Vector3(layout.X, layout.Y, layout.Z);
                    if (NearFieldRadius * NearFieldRadius < (position - playerPos).sqrMagnitude) continue;

                    // 破壊済みは探索から外れるため、位置一致の最近傍が本人であることまでは求めず存在のみ確かめる
                    // A destroyed one drops out of search, so only existence is asserted, not identity of the nearest hit
                    var found = datastore.SearchNearestMapObject(new Guid(layout.MapObjectGuid), position);
                    Assert.IsNotNull(found, $"near-field map object {layout.InstanceId} was not instantiated before the initial-apply wait released");
                    checkedCount++;
                }

                // 近傍0件のワールドでは検証が素通りしてしまうので先に落とす
                // With zero near objects every assertion would pass vacuously, so fail here first
                Assert.Greater(checkedCount, 0, "test world has no map objects within the near field");

                // 全量待機も正規APIとして完走することを固定する
                // Also pin that the full-instantiation wait, the official API, runs to completion
                await datastore.WaitForAllInstantiatedAsync();
            }

            #endregion
        }
    }
}
