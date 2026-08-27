using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.TutorialBlock;
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

                PlaceBlock("無限歯車ジェネレーター", AnchorPosition, BlockDirection.North);
                await WaitBlockGameObjectSpawn(AnchorPosition);

                manager.ApplyTutorial(CreateTutorial("無限歯車ジェネレーター", "シャフト", Offset, "North"));

                // ゴーストはAddressableの非同期ロード後に立つため、生成を待ってから座標を見る
                // The ghost appears after an async Addressable load, so wait for it before reading the position
                TutorialBlockPreviewObject ghost = null;
                for (var i = 0; i < 300 && ghost == null; i++)
                {
                    ghost = manager.GetComponentInChildren<TutorialBlockPreviewObject>(false);
                    await UniTask.Yield();
                }

                Assert.IsNotNull(ghost, "no ghost was shown for the relative placement tutorial");
                Assert.AreEqual(AnchorPosition + Offset, Vector3Int.FloorToInt(ghost.transform.position));
            }

            #endregion
        }

        // テストmodのchallenges.jsonへ相対座標プレビューのチュートリアルを1件差し込み、生成型として取り出す
        // Insert one relative-placement-preview tutorial into the test mod's challenges.json and take it back as the generated type
        private static TutorialsElement CreateTutorial(string anchorBlockName, string blockName, Vector3Int offset, string direction)
        {
            var path = Path.Combine(EditModeInPlayingTestServerDirectoryPath, "mods", "EditModeInPlayingTestMod", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var challenge = (JObject)json["data"][0]["challenges"][0];
            var tutorials = (JArray)challenge["tutorials"];
            tutorials.Clear();
            tutorials.Add(new JObject
            {
                ["tutorialGuid"] = Guid.NewGuid().ToString("D"),
                ["tutorialType"] = "relativeBlockPlacePreview",
                ["tutorialParam"] = new JObject
                {
                    ["anchorBlockGuid"] = FindBlockGuid(anchorBlockName).ToString("D"),
                    ["blockGuid"] = FindBlockGuid(blockName).ToString("D"),
                    ["offset"] = new JArray(offset.x, offset.y, offset.z),
                    ["blockDirection"] = direction,
                    ["message"] = "relative preview test",
                },
            });
            var master = new ChallengeMaster(json);
            master.Initialize();
            return master.GetChallenge(Guid.Parse(challenge["challengeGuid"].Value<string>())).Tutorials[0];
        }

        private static Guid FindBlockGuid(string blockName)
        {
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (master.Name == blockName) return master.BlockGuid;
            }
            throw new InvalidOperationException($"block not found in the test mod: {blockName}");
        }
    }
}
