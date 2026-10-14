#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.Outcrop;
using Game.MapGeneration.Surface;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.Map.Surface
{
    public class OutcropSurfaceSceneIsolationTest
    {
        private readonly List<UnityEngine.Object> _created = new();
        private Scene _competingScene;
        private Scene _placementScene;
        private string _scenePath;
        private string _placementScenePath;

        [TearDown]
        public void TearDown()
        {
            // 失敗時も競合地形とSceneを破棄
            // Release competing terrain and its scene even when assertions fail
            for (var index = _created.Count - 1; 0 <= index; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
            // silent-ok: 初期化失敗時はScene未生成 / NUnitが失敗を報告する
            // An initialization failure is reported by NUnit before a scene exists
            if (_competingScene.IsValid())
                Assert.That(EditorSceneManager.CloseScene(_competingScene, true), Is.True, "Competing scene must close");
            // silent-ok: 初期化失敗時はScene未生成 / NUnitが失敗を報告する
            // NUnit reports initialization failures before the placement scene exists
            if (_placementScene.IsValid())
                Assert.That(EditorSceneManager.CloseScene(_placementScene, true), Is.True, "Placement scene must close");
            Assert.That(AssetDatabase.DeleteAsset(_scenePath), Is.True, "Temporary scene asset must be removed");
            Assert.That(AssetDatabase.DeleteAsset(_placementScenePath), Is.True, "Temporary placement scene asset must be removed");
        }

        [Test]
        public void 別Sceneの重なる地形より同Sceneの非表示地形へ接地する()
        {
            // 既存テストSceneを一時資産へ複製
            // Copy a test scene to a temporary asset for additive loading
            _scenePath = $"Assets/OutcropCompetingTerrain-{Guid.NewGuid()}.unity";
            _placementScenePath = $"Assets/OutcropPlacementTerrain-{Guid.NewGuid()}.unity";
            Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/Other/Test/TextureLoadTest.unity", _scenePath), Is.True, "Temporary scene must copy");
            Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/Other/Test/TextureLoadTest.unity", _placementScenePath), Is.True, "Placement scene must copy");
            _competingScene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            _placementScene = EditorSceneManager.OpenScene(_placementScenePath, OpenSceneMode.Additive);
            var translation = SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero);

            // 小さい原点の異なる高さを競合させる
            // Compete with a lower-origin terrain at a different height
            var competing = CreateTerrain(translation + Vector3.left * 4f, 0f);
            SceneManager.MoveGameObjectToScene(competing, _competingScene);
            var local = CreateTerrain(translation + Vector3.up * 2f, 0.5f);
            SceneManager.MoveGameObjectToScene(local, _placementScene);
            var environment = new GameObject("HiddenEnvironmentFixture");
            _created.Add(environment);
            SceneManager.MoveGameObjectToScene(environment, _placementScene);
            local.transform.SetParent(environment.transform);
            environment.SetActive(false);
            Assert.That(Array.IndexOf(Terrain.activeTerrains, local.GetComponent<Terrain>()), Is.EqualTo(-1));

            // 実配置の底面でScene除外を検証
            // Verify scene exclusion through the bottom produced by real placement
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(root);
            SceneManager.MoveGameObjectToScene(root, _placementScene);
            Assert.That(root.scene, Is.EqualTo(local.scene));
            Assert.That(competing.scene, Is.Not.EqualTo(root.scene));
            var center = translation + new Vector3(16f, 15f, 16f);
            var grounded = new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5);
            var outcrop = OutcropPrefab.Create(root, grounded);
            Assert.That(outcrop.ContractViolation, Is.Null);
            OutcropSurfacePlacement.Place(root, outcrop, new Bounds(center, new Vector3(5f, 6f, 5f)), grounded);
            var groundY = local.GetComponent<Terrain>().SampleHeight(center) + local.transform.position.y;
            Assert.That(root.GetComponent<Renderer>().bounds.min.y - groundY, Is.InRange(0f, 0.02f));
        }

        private GameObject CreateTerrain(Vector3 position, float height)
        {
            // 地形データも後始末へ登録
            // Register terrain data for cleanup as well
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
            _created.Add(data);
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
                for (var x = 0; x < 33; x++) heights[z, x] = height;
            data.SetHeights(0, 0, heights);
            var instance = Terrain.CreateTerrainGameObject(data);
            _created.Add(instance);
            instance.transform.position = position;
            return instance;
        }
    }
}
#endif
