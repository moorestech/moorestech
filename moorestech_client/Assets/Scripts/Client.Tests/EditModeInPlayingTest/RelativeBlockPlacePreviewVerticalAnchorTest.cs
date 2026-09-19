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
    ///     上下向きアンカーで相対ゴーストが例外を投げず未検出扱いになることを実機検証する
    ///     Verifies in a running client that an up/down anchor hides the relative ghost without throwing
    /// </summary>
    [Category("CiShardClientPlay2")]
    public class RelativeBlockPlacePreviewVerticalAnchorTest
    {
        private static readonly Vector3Int AnchorPosition = new(10, 0, 10);
        private static readonly Vector3Int Offset = new(0, 0, 1);

        [UnityTest]
        public IEnumerator アンカーが上下向きだとゴーストが出ず警告が1回だけ出る()
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

                // アンカーを上下向きに置くと、ローカルEastは12方位へ表せず未検出扱いになる
                // Placing the anchor facing up/down makes the local East direction unrepresentable, so it counts as not found
                PlaceBlock("無限歯車ジェネレーター", AnchorPosition, BlockDirection.UpNorth);
                await WaitBlockGameObjectSpawn(AnchorPosition);

                // LogAssert.Expectは回数上限を見ないため自前で数える
                // LogAssert.Expect has no upper bound, so count manually
                var exceptionCount = 0;
                var firstException = "";
                var warningCount = 0;
                var warningPattern = new System.Text.RegularExpressions.Regex(@"\[RelativeBlockPlacePreview\] The nearest anchor faces UpNorth");

                void OnLogMessageReceived(string condition, string stackTrace, LogType type)
                {
                    // 上下向きブロックの外接枠ロードは既存の別件例外を出すため、相対ゴースト経路の例外だけを数える
                    // Loading the bounding box of an up/down block throws a separate existing exception, so count only relative-ghost exceptions
                    var isRelativeGhostException = stackTrace.Contains(nameof(RelativeBlockPlacePreviewTutorialManager)) || stackTrace.Contains(nameof(AnchorRelativeDirectionUtil));
                    if (type == LogType.Exception && isRelativeGhostException && exceptionCount++ == 0) firstException = condition + "\n" + stackTrace;
                    else if (type == LogType.Warning && warningPattern.IsMatch(condition)) warningCount++;
                }

                Application.logMessageReceived += OnLogMessageReceived;

                manager.ApplyTutorial(RelativeBlockPlacePreviewTestSupport.CreateTutorial("無限歯車ジェネレーター", "シャフト", Offset, "East"));

                // 警告が出て未検出扱いになるまで数十フレーム待つ
                // Wait several dozen frames for the warning and the not-found handling to land
                for (var i = 0; i < 60; i++)
                {
                    Assert.IsNull(ghostOwner.GetComponentInChildren<PreviewGhostObject>(false), "a ghost was shown for a vertical anchor");
                    await UniTask.Yield();
                }

                Application.logMessageReceived -= OnLogMessageReceived;

                Assert.AreEqual(0, exceptionCount, $"an exception was thrown while resolving a vertical anchor: {firstException}");
                Assert.AreEqual(1, warningCount, "the vertical anchor warning did not fire exactly once");
            }

            #endregion
        }
    }
}
