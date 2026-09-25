using System;
using System.Collections;
using System.IO;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Player;
using Client.Game.Skit;
using Client.MapScene.Editor;
using Client.Tests.UnitTest.MapPreview.Integration;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay1")]
    public class GeneratedMapPreviewPlayTransitionTest
    {
        private const string FolderKey = "GeneratedMapPreviewPlayTransitionTest.Folder";
        private const string PreviewWorldKey = "GeneratedMapPreviewPlayTransitionTest.PreviewWorld";
        private GeneratedMapPreviewTestFixture _fixture;

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator PendingPreviewClosesOnPlay()
        {
            EnterPlayModeUtil();
            _fixture = new GeneratedMapPreviewTestFixture(false);
            // Playのreloadを跨ぐ所有物の場所はEditorのSessionStateへ記録する
            // Record owned paths in Editor SessionState so they survive Play's domain reload
            var folder = $"Assets/GeneratedMapPreviewPlayTransitionTest_{Guid.NewGuid():N}";
            SessionState.SetString(FolderKey, folder);
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(main, $"{folder}/Main.unity");
            var stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            StageUtility.GoToStage(stage, true);
            EditorApplication.playModeStateChanged += ReleaseFixtureOnPlay;
            stage.Regenerate();
            // 同期生成が終わり、まだ完了していない実走行の境界を待つ
            // Wait for a real in-progress boundary after synchronous generation has finished
            var deadline = EditorApplication.timeSinceStartup + 600;
            while (stage.scene.rootCount == 0 && stage.State == GeneratedMapPreviewState.Generating && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Generating));
            Assert.That(stage.scene.rootCount, Is.EqualTo(1));
            var temporaryRoot = Path.Combine(Application.dataPath, "..", "Temp", "GeneratedMapPreview", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            SessionState.SetString(PreviewWorldKey, Directory.GetDirectories(temporaryRoot).Single());
            Debug.Log("[GeneratedMapPreviewPlayTransition] Actual whole-map generation is pending in EditMode before EnterPlayMode.");

            // 実走行のyield中からPlayへ入り、reloadを跨いで専用一時ワールドの回収を確認する
            // Enter Play during an actual run's yield and verify temporary-world cleanup across reload
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
            Assert.That(Resources.FindObjectsOfTypeAll<GeneratedMapPreviewStage>(), Is.Empty);
            Assert.That(Directory.Exists(SessionState.GetString(PreviewWorldKey, "")), Is.False);

            LogAssert.ignoreFailingMessages = true;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            EditorApplication.playModeStateChanged -= ReleaseFixtureOnPlay;
            _fixture?.Dispose();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 失敗時もPlayを抜けてから、テストが所有するSceneをEditor経由で回収する
            // Even after failure, leave Play before reclaiming the test-owned scene through the Editor
            LogAssert.ignoreFailingMessages = true;
            EditorApplication.playModeStateChanged -= ReleaseFixtureOnPlay;
            if (!EditorApplication.isPlaying) StageUtility.GoToMainStage();
            _fixture?.Dispose();
            _fixture = null;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var folder = SessionState.GetString(FolderKey, "");
            if (0 < folder.Length) AssetDatabase.DeleteAsset(folder);
            SessionState.EraseString(FolderKey);
            SessionState.EraseString(PreviewWorldKey);
            LogAssert.ignoreFailingMessages = false;
        }

        private void ReleaseFixtureOnPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            EditorApplication.playModeStateChanged -= ReleaseFixtureOnPlay;
            _fixture.Dispose();
            _fixture = null;
        }
    }
}
