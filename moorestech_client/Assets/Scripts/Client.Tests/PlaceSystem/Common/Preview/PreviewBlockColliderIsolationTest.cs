#if UNITY_EDITOR
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PreviewBlockColliderIsolationTest
    {
        private Scene _scene;
        private PreviewGhostViaControllerFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _scene = EditorSceneManager.NewPreviewScene();
            _fixture = new PreviewGhostViaControllerFixture();
            _fixture.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Dispose();
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void 本番入口で作ったゴーストは有効化とプール再利用でも全Colliderが無効のまま()
        {
            var preview = _fixture.SetPreview();

            // 非表示だった子のColliderも本番経路で無効化済み
            // Colliders of the formerly dormant child are already disabled by the production path
            var ghost = preview.gameObject;
            var dormant = ghost.transform.Find(_fixture.DormantChild.name).gameObject;
            Assert.IsNotEmpty(dormant.GetComponents<Collider>());
            dormant.SetActive(true);
            AssertAllCollidersDisabled(ghost);

            // プールを解放して再利用しても当たり判定は復活しない
            // Releasing and reusing the pooled ghost must not restore collisions
            var reused = _fixture.SetPreview();
            Assert.AreSame(preview, reused);
            AssertAllCollidersDisabled(reused.gameObject);
        }

        private static void AssertAllCollidersDisabled(GameObject ghost)
        {
            var colliders = ghost.GetComponentsInChildren<Collider>(true);
            Assert.GreaterOrEqual(colliders.Length, 3);
            foreach (var collider in colliders) Assert.IsFalse(collider.enabled, collider.name);
        }

        [TestCase("metal_gear belt conveyor")]
        [TestCase("metal_gear belt conveyor splitter")]
        [TestCase("Old/wooden chest")]
        [TestCase("Charcoal_Kiln")]
        [TestCase("Primitive_Blast_Furnace")]
        [TestCase("Sieve")]
        [TestCase("campfier")]
        [TestCase("press machine")]
        public void PreviewHasNoRigidbody(string prefabName)
        {
            var ghost = InstantiatePreview(prefabName);

            Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>(true), prefabName);
        }

        private GameObject InstantiatePreview(string prefabName)
        {
            // 実Prefabを隔離シーンへ複製し、元アセットへの変更を防ぐ
            // Clone the real prefab into an isolated scene to protect the source asset
            var path = $"Assets/AddressableResources/Block/{prefabName}.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, path);
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            ghost.SetActive(true);
            return ghost;
        }
    }
}
#endif
