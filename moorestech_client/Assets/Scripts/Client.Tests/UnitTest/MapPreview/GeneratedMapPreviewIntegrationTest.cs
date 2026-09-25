using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.MapScene.Editor;
using Client.Tests.UnitTest.MapPreview.Integration;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class GeneratedMapPreviewIntegrationTest
    {
        private string _assetFolder;
        private Scene _main;
        private GeneratedMapPreviewStage _stage;
        private GeneratedMapPreviewTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
            _assetFolder = $"Assets/GeneratedMapPreviewIntegrationTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring(7));
            _fixture = new GeneratedMapPreviewTestFixture(false, 1);
            // Runnerのbootstrapを保存可能なfixtureへ替え、主Sceneの保持を実測する
            // Replace the runner bootstrap with a savable fixture to measure main-scene preservation
            _main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("PreservedMainObject");
            EditorSceneManager.SaveScene(_main, $"{_assetFolder}/Main.unity");
            _stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            StageUtility.GoToStage(_stage, true);
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_assetFolder);
            _fixture.Dispose();
        }

        [UnityTest]
        [Timeout(1400000)]
        public IEnumerator TrackedFixtureMatchesEveryPlacementAndTileAndRegenerationReleasesPreviousRuns()
        {
            var baselineData = Resources.FindObjectsOfTypeAll<TerrainData>().Select(x => x.GetInstanceID()).ToHashSet();
            var baselineDirectories = GeneratedMapPreviewObservation.WorldDirectories();
            var objectCount = _main.GetRootGameObjects().Sum(x => x.GetComponentsInChildren<Transform>(true).Length);
            GameObject previousRoot = null;
            TerrainData[] previousData = Array.Empty<TerrainData>();
            string previousDirectory = null;
            for (var generation = 0; generation < 3; generation++)
            {
                // 同じStageで再生成し、前回分の破棄を次回完了より先に検査する
                // Regenerate within one stage and inspect prior ownership before the next run completes
                _stage.Regenerate();
                Assert.That(previousRoot == null && previousData.All(x => x == null), Is.True);
                if (previousDirectory != null) Assert.That(Directory.Exists(previousDirectory), Is.False);
                yield return GeneratedMapPreviewObservation.WaitForCompletion(_stage);
                Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Ready), _stage.StatusText);
                var root = _stage.scene.GetRootGameObjects().Single();
                var data = root.GetComponentsInChildren<UnityEngine.Terrain>().Select(x => x.terrainData).ToArray();
                var directory = GeneratedMapPreviewObservation.WorldDirectories().Except(baselineDirectories).Single();
                var files = WorldDataDirectory.FromWorldRoot(directory);
                var session = (TiledTerrainSession)WorldTerrainSession.Open(TerrainTransferMetaReader.Read(files), _fixture.ServerDataDirectory);

                // 実走行のmap.jsonと転送メタを使い、別生成の結果で代用しない
                // Read the actual run's map and transfer metadata rather than substituting another generation
                GeneratedMapPreviewPlacementParity.AssertMatches(root, files.MapJsonFilePath);
                yield return GeneratedMapPreviewTerrainParity.AssertMatches(root, session);
                // 全走行で所有資源の総数と主Sceneの不変条件を確認する
                // Check total owned resources and main-scene invariants on every run
                Assert.That(data.Length, Is.EqualTo(session.Layout.TileCoordinates.Count));
                Assert.That(Resources.FindObjectsOfTypeAll<TerrainData>().Count(x => !baselineData.Contains(x.GetInstanceID())), Is.EqualTo(data.Length));
                Assert.That(EditorApplication.isPlaying, Is.False);
                Assert.That(_main.isDirty, Is.False);
                Assert.That(_main.GetRootGameObjects().Sum(x => x.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(objectCount));
                previousRoot = root;
                previousData = data;
                previousDirectory = directory;
                if (generation == 2)
                {
                    StageUtility.GoToMainStage();
                    Assert.That(root == null && data.All(x => x == null), Is.True);
                    Assert.That(Directory.Exists(directory), Is.False);
                }
            }
            Assert.That(GeneratedMapPreviewObservation.WorldDirectories(), Is.EquivalentTo(baselineDirectories));
            Assert.That(Resources.FindObjectsOfTypeAll<TerrainData>().Where(x => !baselineData.Contains(x.GetInstanceID())), Is.Empty);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_main));
            Assert.That(_main.isDirty, Is.False);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ClosingAfterWholeWorldCreationReclaimsPendingGeneration()
        {
            var baseline = GeneratedMapPreviewObservation.WorldDirectories();
            _stage.Regenerate();
            // 同期生成から最初のTerrain処理へ渡った時点を上限付きで待つ
            // Wait with a deadline for synchronous generation to hand over to the first terrain build
            var deadline = EditorApplication.timeSinceStartup + 600;
            while (_stage.scene.rootCount == 0 && _stage.State == GeneratedMapPreviewState.Generating && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Generating));
            var root = _stage.scene.GetRootGameObjects().Single();
            var data = Resources.FindObjectsOfTypeAll<TerrainData>().Where(x => !AssetDatabase.Contains(x)).ToArray();
            Assert.That(GeneratedMapPreviewObservation.WorldDirectories().Except(baseline), Is.Not.Empty);

            // 同期ワールド生成が戻った後の実際のyield境界で閉じる
            // Close at a real yield boundary after synchronous whole-world provisioning returns
            StageUtility.GoToMainStage();
            yield return null;
            Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Closed));
            Assert.That(root == null, Is.True);
            Assert.That(data.All(x => x == null), Is.True);
            Assert.That(GeneratedMapPreviewObservation.WorldDirectories(), Is.EquivalentTo(baseline));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_main));
        }

        [UnityTest]
        public IEnumerator FirstAlphamapYieldCanCancelThenDisposeContentTwice()
        {
            var originalScene = SceneManager.GetActiveScene();
            var wasDirty = originalScene.isDirty;
            using var content = new GeneratedMapPreviewContent(_stage.scene);
            using var cancellation = new CancellationTokenSource();
            var layer = new TerrainLayer();
            var data = content.CreateTerrainData();
            try
            {
                var tile = new BakedTerrainTile(Vector3.zero, new float[33, 33], TileAlphamap.Create(new[] { new byte[16 * 16 * 4] }, 16, 1), Array.Empty<int[,]>());
                var operation = TerrainAlphamapApplier.ApplyAsync(data, new[] { layer }, tile, cancellation.Token);
                Assert.That(operation.Status, Is.EqualTo(UniTaskStatus.Pending));
                cancellation.Cancel();
                content.Dispose();
                content.Dispose();
                Exception observed = null;
                yield return operation.ToCoroutine(exception => observed = exception);
                Assert.That(observed, Is.TypeOf<OperationCanceledException>());
                Assert.That(data == null, Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
                Assert.That(originalScene.isDirty, Is.EqualTo(wasDirty));
            }
            finally { Object.DestroyImmediate(layer); }
        }
    }
}
