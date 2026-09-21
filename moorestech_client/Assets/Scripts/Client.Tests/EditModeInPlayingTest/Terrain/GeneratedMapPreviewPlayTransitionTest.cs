using System;
using System.Collections;
using System.IO;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Player;
using Client.Game.Skit;
using Client.MapScene.Editor;
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
        private const string GameWorldKey = "GeneratedMapPreviewPlayTransitionTest.GameWorld";

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator PendingPreviewClosesOnPlayAndOrdinaryGeneratedTerrainStillInitializes()
        {
            EnterPlayModeUtil();
            // Playのreloadを跨ぐ所有物の場所はEditorのSessionStateへ記録する
            // Record owned paths in Editor SessionState so they survive Play's domain reload
            var folder = $"Assets/GeneratedMapPreviewPlayTransitionTest_{Guid.NewGuid():N}";
            SessionState.SetString(FolderKey, folder);
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(main, $"{folder}/Main.unity");
            var stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            StageUtility.GoToStage(stage, true);
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
            yield return VerifyOrdinaryGame().ToCoroutine();

            LogAssert.ignoreFailingMessages = true;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 失敗時もPlayを抜けてから、テストが所有するSceneをEditor経由で回収する
            // Even after failure, leave Play before reclaiming the test-owned scene through the Editor
            LogAssert.ignoreFailingMessages = true;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var folder = SessionState.GetString(FolderKey, "");
            if (0 < folder.Length) AssetDatabase.DeleteAsset(folder);
            var world = SessionState.GetString(GameWorldKey, "");
            if (0 < world.Length && Directory.Exists(world)) Directory.Delete(world, true);
            SessionState.EraseString(FolderKey);
            SessionState.EraseString(PreviewWorldKey);
            SessionState.EraseString(GameWorldKey);
            LogAssert.ignoreFailingMessages = false;
        }

        private static async UniTask VerifyOrdinaryGame()
        {
            var initialized = false;
            using var subscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);
            var world = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", $"PreviewTerrainRegression_{Guid.NewGuid():N}"));
            SessionState.SetString(GameWorldKey, world);
            await LoadMainGameWithMapMode(Server.Boot.ServerDirectory.GetDirectory(), world, WorldMapMode.Generated);
            await UniTask.WaitUntil(() => initialized).Timeout(TimeSpan.FromSeconds(180));

            // 実マスタの導入スキットは背景を隠すため、公開操作で終了して通常描画へ戻す
            // The real-master intro hides the environment, so finish it through its public action before checking ordinary rendering
            var skit = UnityEngine.Object.FindFirstObjectByType<SkitManager>();
            if (skit.IsPlayingSkit)
            {
                var store = SkitPresentationStateStore.Instance;
                await UniTask.WaitUntil(() => store.GetCurrent().AllowedIntents.Contains("skip")).Timeout(TimeSpan.FromSeconds(60));
                var current = store.GetCurrent();
                Assert.That(store.TrySkip(current.SessionId, current.SceneRevision).Ok, Is.True);
                await UniTask.WaitUntil(() => !skit.IsPlayingSkit).Timeout(TimeSpan.FromSeconds(60));
                Debug.Log("[GeneratedMapPreviewPlayTransition] Real-master intro skipped through the public presentation action; ordinary environment restored.");
            }

            // 通常初期化の完了を待ち、共有部品が材質・草・Colliderを組み立てた実物を検査する
            // Wait for ordinary initialization and inspect real terrain material, grass, and colliders built by shared components
            var terrains = UnityEngine.Terrain.activeTerrains;
            Assert.That(terrains, Is.Not.Empty);
            foreach (var terrain in terrains)
            {
                Assert.That(terrain.terrainData.heightmapResolution, Is.GreaterThan(32));
                Assert.That(terrain.terrainData.terrainLayers, Is.Not.Empty);
                Assert.That(terrain.terrainData.detailPrototypes, Is.Not.Empty);
                Assert.That(terrain.materialTemplate.shader.name, Does.Contain("Terrain"));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(terrain.terrainData));
            }
            var position = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            Assert.That(GroundHeightProbe.TryGetGroundPoint(position.x, position.z, out var ground), Is.True);
            Debug.Log($"[GeneratedMapPreviewPlayTransition] Pending preview was reclaimed; normal generated game initialized with {terrains.Length} terrain(s), ground {ground}, player {position}.");
        }
    }
}
