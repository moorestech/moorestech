using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Client.MapScene.Editor;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class GeneratedMapPreviewPendingCloseTest
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene _mainScene;
        private string _assetFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
            _assetFolder = $"Assets/GeneratedMapPreviewPendingCloseTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring("Assets/".Length));
            // Test Runnerの未保存bootstrapを、復元検証に使える保存済みSceneへ置き換える
            // Replace the test runner's unsaved bootstrap with a saved scene for restoration assertions
            _mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(_mainScene, $"{_assetFolder}/Main.unity");
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_assetFolder);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CloseRetainsPendingGenerationUntilItCompletes(bool failAfterResume)
        {
            var previewCount = EditorSceneManager.previewSceneCount;
            var stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            StageUtility.GoToStage(stage, true);
            var stageScene = stage.scene;
            var content = new GeneratedMapPreviewContent(stageScene);
            var root = content.Root.gameObject;
            var data = content.CreateTerrainData();
            var layer = new TerrainLayer();
            data.terrainLayers = new[] { layer };
            var files = WorldDataDirectory.FromWorldRoot(Path.Combine(Application.dataPath, "..", "Temp", $"PreviewPendingTest_{Guid.NewGuid():N}"));
            Directory.CreateDirectory(files.Root);
            Directory.CreateDirectory(files.ProvisioningTempDirectory);
            var world = (GeneratedMapPreviewWorld)Activator.CreateInstance(typeof(GeneratedMapPreviewWorld),
                PrivateInstance, null, new object[] { files }, null);
            using var run = new GeneratedMapPreviewRun();
            using var cancellation = new CancellationTokenSource();
            var cancellationToken = cancellation.Token;
            var release = new UniTaskCompletionSource();

            // 私有の実行監視へ制御可能なawaitを渡し、テスト専用公開APIを追加しない
            // Supply a controlled await to the private execution observer without adding a public test API
            SetField(run, "_content", content);
            SetField(run, "_world", world);
            SetField(stage, "_run", run);
            SetField(stage, "_cancellation", cancellation);
            typeof(GeneratedMapPreviewStage).GetProperty(nameof(stage.State)).SetValue(stage, GeneratedMapPreviewState.Generating);
            var observe = typeof(GeneratedMapPreviewStage).GetMethod("GenerateAsync", PrivateInstance);
            var completion = ((UniTask)observe.Invoke(stage, new object[] { run, ResumeUsingOwnedResourcesAsync(), cancellationToken })).AsTask();
            try
            {
                Assert.That(completion.IsCompleted, Is.False);
                StageUtility.GoToMainStage();
                EditorUtility.UnloadUnusedAssetsImmediate();
                var closedStatus = stage.StatusText;
                Assert.That(cancellationToken.IsCancellationRequested, Is.True);
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Closed));
                Assert.That(stageScene.IsValid(), Is.False);
                Assert.That(completion.IsCompleted, Is.False);
                Assert.That(root != null, Is.True, "Close must retain the root while the execution is pending.");
                Assert.That(data != null, Is.True, "Close must retain native TerrainData while the execution is pending.");
                var holdingScene = root.scene;
                Assert.That(EditorSceneManager.IsPreviewScene(holdingScene), Is.True);
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount + 1));
                Assert.That(Directory.Exists(files.Root) && Directory.Exists(files.ProvisioningTempDirectory), Is.True);

                // キャンセルをまだ観測しない継続でも資源へ触れ、終了した後だけ全所有物を解放する
                // Let a continuation that has not observed cancellation use its resources and release them only at termination
                release.TrySetResult();
                Assert.That(completion.IsCompleted, Is.True);
                if (failAfterResume) Assert.Throws<InvalidOperationException>(() => completion.GetAwaiter().GetResult());
                else Assert.Throws<OperationCanceledException>(() => completion.GetAwaiter().GetResult());
                Assert.That(root == null && data == null, Is.True);
                Assert.That(holdingScene.IsValid(), Is.False);
                Assert.That(Directory.Exists(files.Root) || Directory.Exists(files.ProvisioningTempDirectory), Is.False);
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Closed));
                Assert.That(stage.StatusText, Is.EqualTo(closedStatus));
                run.Dispose();
                Assert.That(layer != null, Is.True);
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_mainScene));
                Assert.That(_mainScene.isDirty, Is.False);
            }
            finally
            {
                StageUtility.GoToMainStage();
                release.TrySetResult();
                if (completion.IsFaulted) _ = completion.Exception;
                run.Dispose();
                Object.DestroyImmediate(layer);
            }

            #region Internal

            async UniTask ResumeUsingOwnedResourcesAsync()
            {
                await release.Task;
                root.name = "ResumedPendingPreview";
                data.heightmapResolution = 33;
                Assert.That(Directory.Exists(files.Root), Is.True);
                if (failAfterResume) throw new InvalidOperationException("Controlled generation failure after Close.");
            }

            #endregion
        }

        [Test]
        public void InitialGenerationYieldCancelsWithoutAnotherEditorUpdate()
        {
            using var run = new GeneratedMapPreviewRun();
            using var cancellation = new CancellationTokenSource();
            var execution = run.ExecuteAsync(default, cancellation.Token).AsTask();
            Assert.That(execution.IsCompleted, Is.False);
            cancellation.Cancel();
            Assert.That(execution.IsCompleted, Is.True, "Reload and Play transitions may prevent another editor update.");
            Assert.Throws<OperationCanceledException>(() => execution.GetAwaiter().GetResult());
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }
    }
}
