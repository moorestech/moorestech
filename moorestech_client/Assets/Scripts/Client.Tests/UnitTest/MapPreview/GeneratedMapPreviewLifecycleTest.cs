using System;
using Client.MapScene.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class GeneratedMapPreviewLifecycleTest
    {
        private Scene _mainScene;
        private string _assetFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
            _assetFolder = $"Assets/GeneratedMapPreviewLifecycleTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring("Assets/".Length));
            // Test Runnerの未保存bootstrapはadditive作成不可なので、fixture専用Sceneへ切り替える
            // The test runner's unsaved bootstrap prevents additive creation, so replace it with a fixture scene
            _mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(_mainScene);
            EditorSceneManager.SaveScene(_mainScene, $"{_assetFolder}/Main.unity");
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_assetFolder);
        }

        [Test]
        public void ContentDisposesOnlyItsRootAndTerrainDataAndCanBeRecreated()
        {
            var unrelated = new GameObject("UnrelatedMainObject");
            EditorSceneManager.SaveScene(_mainScene);
            var preview = EditorSceneManager.NewPreviewScene();
            var content = new GeneratedMapPreviewContent(preview);
            var root = content.Root.gameObject;
            var data = content.CreateTerrainData();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/AddressableResources/Environment/Terrain/TerrainLitMaterial.mat");
            AssetDatabase.CreateAsset(new TerrainLayer(), $"{_assetFolder}/BorrowedLayer.terrainlayer");
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{_assetFolder}/BorrowedLayer.terrainlayer");
            try
            {
                // 借用Materialを載せても、破棄は所有rootと自前TerrainDataに限定する
                // Borrowing a material must still limit destruction to the owned root and terrain data
                var terrain = new GameObject("OwnedTerrain");
                terrain.transform.SetParent(content.Root, false);
                data.terrainLayers = new[] { layer };
                terrain.AddComponent<UnityEngine.Terrain>().terrainData = data;
                terrain.GetComponent<UnityEngine.Terrain>().materialTemplate = material;
                Assert.That(root.scene, Is.EqualTo(preview));
                Assert.That(terrain.scene, Is.EqualTo(preview));
                content.Dispose();
                content.Dispose();
                Assert.That(root == null && data == null, Is.True);
                Assert.That(preview.GetRootGameObjects(), Is.Empty);
                Assert.That(unrelated != null && material != null && layer != null, Is.True);
                Assert.That(AssetDatabase.Contains(material), Is.True);
                Assert.That(AssetDatabase.Contains(layer), Is.True);
                Assert.That(_mainScene.isDirty, Is.False);
                Assert.Throws<ObjectDisposedException>(() => content.CreateTerrainData());

                // 同じSceneへ新しい所有者を作っても、古いDisposeは干渉しない
                // Recreating an owner in the same scene must survive disposing the previous owner again
                using var replacement = new GeneratedMapPreviewContent(preview);
                var replacementData = replacement.CreateTerrainData();
                content.Dispose();
                Assert.That(replacement.Root != null && replacementData != null, Is.True);
                Assert.That(preview.rootCount, Is.EqualTo(1));
                replacement.Dispose();
                Assert.That(replacementData == null, Is.True);
            }
            finally
            {
                content.Dispose();
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StageRestoresSceneDirtyActiveSceneLightingAndCamera(bool initiallyDirty)
        {
            if (initiallyDirty) EditorSceneManager.MarkSceneDirty(_mainScene);
            var view = ScriptableObject.CreateInstance<SceneView>();
            view.Show();
            var pivot = new Vector3(13f, 7f, -21f);
            var rotation = Quaternion.Euler(20f, 125f, 0f);
            view.LookAt(pivot, rotation, 67f, true, true);
            view.sceneLighting = false;
            var previewCount = EditorSceneManager.previewSceneCount;
            var stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            try
            {
                StageUtility.GoToStage(stage, true);
                var previewScene = stage.scene;
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Empty));
                Assert.That(previewScene.IsValid(), Is.True);
                Assert.That(view.sceneLighting, Is.True);
                view.LookAt(Vector3.zero, Quaternion.identity, 4f, false, true);
                view.sceneLighting = true;
                StageUtility.GoToMainStage();

                // 保存済み/未保存の主Sceneとカメラを、Stage開閉の前後で比較する
                // Compare both saved and dirty main scenes and their camera before and after stage navigation
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Closed));
                Assert.That(previewScene.IsValid(), Is.False);
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_mainScene));
                Assert.That(_mainScene.isDirty, Is.EqualTo(initiallyDirty));
                Assert.That(view.sceneLighting, Is.False);
                Assert.That(Vector3.Distance(view.pivot, pivot), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(view.rotation, rotation), Is.LessThan(0.001f));
                Assert.That(view.size, Is.EqualTo(67f).Within(0.001f));
                Assert.That(view.orthographic, Is.True);
            }
            finally
            {
                StageUtility.GoToMainStage();
                if (stage != null) Object.DestroyImmediate(stage);
                view.Close();
            }
        }

        [Test]
        public void OpeningWindowDoesNotOpenStageOrGenerateContent()
        {
            var previewCount = EditorSceneManager.previewSceneCount;
            var window = ScriptableObject.CreateInstance<GeneratedMapPreviewWindow>();
            try
            {
                window.Show();
                Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
                Assert.That(_mainScene.rootCount, Is.Zero);
                Assert.That(_mainScene.isDirty, Is.False);
            }
            finally { window.Close(); }
        }
    }
}
