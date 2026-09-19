using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    ///     テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    ///     相対座標ゴーストが最寄りアンカーの原点＋offsetに立つことを実機検証する
    ///     This test runs in EditMode but switches to PlayMode during execution.
    ///     Verifies that the relative ghost stands at nearest-anchor origin + offset in a real running client.
    /// </summary>
    public class RelativeBlockPlacePreviewTest
    {
        private static readonly Vector3Int AnchorPosition = new(10, 0, 10);
        private static readonly Vector3Int Offset = new(0, 0, 1);

        [UnityTest]
        public IEnumerator アンカー設置後にゴーストがアンカー原点プラスoffsetへ出る()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var manager = Object.FindFirstObjectByType<RelativeBlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
                Assert.IsNotNull(manager, "the scene has no RelativeBlockPlacePreviewTutorialManager");

                // ゴーストの実体はBlockPlacePreviewTutorialManagerが持つ。相対側は目標セルを押し出すだけ
                // The ghost itself lives under BlockPlacePreviewTutorialManager; the relative side only pushes the target cell
                var ghostOwner = Object.FindFirstObjectByType<BlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
                Assert.IsNotNull(ghostOwner, "the scene has no BlockPlacePreviewTutorialManager");

                PlaceBlock("無限歯車ジェネレーター", AnchorPosition, BlockDirection.North);
                await WaitBlockGameObjectSpawn(AnchorPosition);

                manager.ApplyTutorial(RelativeBlockPlacePreviewTestSupport.CreateTutorial("無限歯車ジェネレーター", "シャフト", Offset, "North"));

                // ゴーストはAddressableの非同期ロード後に立つため、生成を待ってから座標を見る
                // The ghost appears after an async Addressable load, so wait for it before reading the position
                PreviewGhostObject ghost = null;
                for (var i = 0; i < 300 && ghost == null; i++)
                {
                    ghost = ghostOwner.GetComponentInChildren<PreviewGhostObject>(false);
                    await UniTask.Yield();
                }

                Assert.IsNotNull(ghost, "no ghost was shown for the relative placement tutorial");
                Assert.AreEqual(AnchorPosition + Offset, Vector3Int.FloorToInt(ghost.transform.position));
            }

            #endregion
        }

        [UnityTest]
        public IEnumerator ロード中に完了したゴーストは点灯しない()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var manager = Object.FindFirstObjectByType<RelativeBlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
                Assert.IsNotNull(manager, "the scene has no RelativeBlockPlacePreviewTutorialManager");

                var ghostOwner = Object.FindFirstObjectByType<BlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
                Assert.IsNotNull(ghostOwner, "the scene has no BlockPlacePreviewTutorialManager");

                PlaceBlock("無限歯車ジェネレーター", AnchorPosition, BlockDirection.North);
                await WaitBlockGameObjectSpawn(AnchorPosition);

                // Addressableロードが終わる前に完了させる。1フレームしか進めないのがこのテストの肝
                // Complete before the Addressable load finishes; advancing only one frame is the point of this test
                var view = manager.ApplyTutorial(RelativeBlockPlacePreviewTestSupport.CreateTutorial("無限歯車ジェネレーター", "シャフト", Offset, "North"));
                await UniTask.Yield();
                view.CompleteTutorial();

                // 遅れて着地したゴーストが後から点灯しないことを見る
                // Watches that a late-landing ghost never lights up afterwards
                for (var i = 0; i < 300; i++)
                {
                    Assert.IsNull(ghostOwner.GetComponentInChildren<PreviewGhostObject>(false), "a ghost lit up after the tutorial had completed");
                    await UniTask.Yield();
                }
            }

            #endregion
        }
    }
}
