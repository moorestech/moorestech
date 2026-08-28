using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// 起動シーケンス全体を実機で通し、自機が構築済み地形の上でハンドシェイク座標に置かれることと、初期イベント適用の待機対象がDIから1本も漏れないことを検証する
    /// 起動中のLogErrorを握り潰さないことが本テストの主眼なので、ignoreFailingMessagesはEnterPlayMode直後と後片付けだけに限る
    /// Runs the whole startup sequence for real, verifying the player lands at its handshake position over built terrain and that no initial-event wait target is missing from DI.
    /// Leaving startup LogErrors unswallowed is the point of this test, so ignoreFailingMessages is confined to the EnterPlayMode frame and teardown.
    /// </summary>
    public class PlayerStartsOnBuiltTerrainTest
    {
        // 地表下に埋まった自機はCharacterControllerの押し出しでXZが数cm動く。Warpが抜ければ数百mずれるので1mで切れる
        // A player buried under the surface drifts a few centimeters in XZ from CharacterController depenetration, while a missing warp lands hundreds of meters away, so one meter separates them
        private const float HorizontalTolerance = 1f;

        // 新規ワールドのスポーンYは0で地表下にあり、自機は起動直後からそこで2m/sで沈み続ける。落下復帰は逆に地表まで数十m跳ね上げるので、その手前で切る
        // A new world spawns at Y=0 below the surface and the player sinks there at 2 m/s from the start, while a fall recovery instead snaps it tens of meters up to the surface, so the bound sits below that
        private const float HandshakePositionToleranceY = 10f;

        // PlayerObjectControllerが落下復帰を発動する高さ。起動中にここを割ったら地形の無い空間へ解放されている
        // The height at which PlayerObjectController triggers fall recovery; dipping below it during startup means release into a terrain-less space
        private const float FallRecoveryThresholdY = -50f;

        private const int InitializationTimeoutSeconds = 180;

        [UnityTest]
        public IEnumerator PlayerStandsAtHandshakePositionAndEveryWaitTargetIsRegistered()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // 見逃すのはEnterPlayMode直後のテストフレームワーク内部エラーだけ。起動シーケンスのLogErrorは以降すべて検出する
            // Only the test framework's own errors right after EnterPlayMode are ignored; every startup LogError after this is detected
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            yield return Body().ToCoroutine();

            // PlayMode終了でローカルサーバのソケットが閉じ、受信スレッドが必ず切断エラーを吐く。検証は上で終わっている
            // Exiting PlayMode closes the local server socket and the receive thread always logs a disconnect error; every assertion is already done above
            LogAssert.ignoreFailingMessages = true;

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                // 初期化完了はGameInitializedEventでしか分からない。LoadMainGameは固定1秒で戻り地形構築の完了を待たない
                // GameInitializedEvent is the only completion signal; LoadMainGame returns after a fixed second without waiting for the terrain build
                var isInitialized = false;
                using var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => isInitialized = true);

                // 地形構築より前に解放されていないことを見る。健全時は解放後を1フレームも観測せず、守備範囲は解放前区間に限られる
                // Watches that nothing is released before the terrain is built; a healthy run observes no post-release frame, so its coverage is the pre-release interval only
                var tracing = TraceLowestPlayerYUntilInitialized();

                var worldDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_player_start_terrain_test_{Guid.NewGuid()}");
                await LoadMainGameWithMapMode(null, worldDirectory, WorldMapMode.Generated);
                await UniTask.WaitUntil(() => isInitialized).Timeout(TimeSpan.FromSeconds(InitializationTimeoutSeconds));

                AssertPlayerStandsOnTerrain(await tracing);
                AssertEveryProductionWaitTargetIsResolved();

                // runごとに新しいworldIdのワールドと地形キャッシュが増え続けるので、検証を終えた分をその場で消す
                // Every run leaves behind another worldId's world and terrain cache, so the finished ones are deleted right here
                var worldId = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>().MapLayout.TerrainMeta.WorldId;
                Directory.Delete(GameSystemPaths.GetWorldCacheDirectory(worldId), true);
                Directory.Delete(worldDirectory, true);

                async UniTask<float> TraceLowestPlayerYUntilInitialized()
                {
                    var lowestY = float.MaxValue;
                    while (!isInitialized)
                    {
                        // 自機はシーンロード後に生えるので、現れるまでは記録対象が無い
                        // The player appears only after the scene loads, so there is nothing to record until then
                        if (PlayerSystemContainer.Instance != null)
                            lowestY = Mathf.Min(lowestY, PlayerSystemContainer.Instance.PlayerObjectController.Position.y);
                        await UniTask.Yield();
                    }
                    return lowestY;
                }
            }

            void AssertPlayerStandsOnTerrain(float lowestObservedY)
            {
                var handshakePosition = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>().PlayerPos;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var hasGround = GroundHeightProbe.TryGetGroundPoint(playerPosition.x, playerPosition.z, out var groundPoint);
                Debug.Log($"[PlayerStartsOnBuiltTerrainTest] handshake:{handshakePosition} player:{playerPosition} ground:{hasGround}/{groundPoint} lowestY:{lowestObservedY}");

                // 地形構築より前に解放されていれば、この閾値を割った上でスポーンへ飛ばされる
                // Being released before the terrain is built drops the player past this threshold and then flings it to the spawn
                Assert.Greater(lowestObservedY, FallRecoveryThresholdY, "起動中に自機が落下復帰の閾値まで落ちている");

                // ハンドシェイク座標へ置かれたこと。StartPlayerRuntimeが呼ばれなければシーン配置座標のまま残る
                // It was placed at the handshake position; without StartPlayerRuntime it would stay at the scene-authored position
                Assert.AreEqual(handshakePosition.x, playerPosition.x, HorizontalTolerance, $"自機Xがハンドシェイク座標と違う actual:{playerPosition}");
                Assert.AreEqual(handshakePosition.z, playerPosition.z, HorizontalTolerance, $"自機Zがハンドシェイク座標と違う actual:{playerPosition}");
                Assert.AreEqual(handshakePosition.y, playerPosition.y, HandshakePositionToleranceY, $"自機Yがハンドシェイク座標から離れている actual:{playerPosition}");

                // そのXZに地形が構築済みであること。レイは上空から落とすので接地ではなく地形の有無を見ている
                // Terrain is built at that XZ; the ray falls from high above, so this checks terrain existence rather than ground contact
                Assert.IsTrue(hasGround, $"自機のXZに地形が構築されていない pos:{playerPosition}");
            }

            // Finalizerが実際に使うコンテナから解決する。別に組んだコンテナでassertしても登録漏れは検出できない
            // Resolve from the very container the finalizer uses; asserting against a separately built container would not detect a missing registration
            void AssertEveryProductionWaitTargetIsResolved()
            {
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var resolvedTypes = resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>().Select(target => target.GetType()).ToList();

                // 件数assertでは4本目を足して登録し忘れたケースを通してしまうので、実装型を列挙して全数を突き合わせる
                // A count assertion would pass a forgotten fourth registration, so every implementation type is enumerated and matched
                foreach (var implementationType in CollectProductionWaitTargetTypes())
                    CollectionAssert.Contains(resolvedTypes, implementationType, $"{implementationType.Name}が初期イベント適用の待機対象としてDI登録されていない");
            }

            IReadOnlyList<Type> CollectProductionWaitTargetTypes()
            {
                // interfaceを宣言するアセンブリを参照しない実装は存在し得ないため、走査範囲をそこへ限る
                // No implementation can exist without referencing the assembly that declares the interface, so the scan is limited to those
                var declaringAssemblyName = typeof(IInitialEventApplyWaitTarget).Assembly.GetName().Name;
                var implementationTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == declaringAssemblyName || assembly.GetReferencedAssemblies().Any(reference => reference.Name == declaringAssemblyName))
                    .Where(IsProductionAssembly)
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => !type.IsAbstract && typeof(IInitialEventApplyWaitTarget).IsAssignableFrom(type))
                    .ToList();

                // 走査が空振りしたまま素通りすると、上のassertが名目だけになる
                // A scan that silently finds nothing would reduce the assertion above to a formality
                Assert.IsNotEmpty(implementationTypes, "プロダクション実装の走査が1件も拾えていない");
                return implementationTypes;
            }

            // テストアセンブリのFakeWaitTargetが期待集合を汚染するのを防ぐ
            // Keeps the test assembly's FakeWaitTarget from polluting the expected set
            bool IsProductionAssembly(Assembly assembly)
            {
                return assembly.GetReferencedAssemblies().All(reference => reference.Name != "nunit.framework");
            }

            #endregion
        }
    }
}
