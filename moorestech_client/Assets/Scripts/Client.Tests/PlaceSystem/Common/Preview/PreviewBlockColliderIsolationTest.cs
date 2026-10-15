#if UNITY_EDITOR
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PreviewBlockColliderIsolationTest
    {
        private Scene _scene;

        [SetUp]
        public void SetUp() => _scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

        [TestCase("metal_gear belt conveyor")]
        [TestCase("metal_gear belt conveyor splitter")]
        [TestCase("Old/wooden chest")]
        public void AllCollidersStayDisabledAcrossActivationAndReuse(string prefabName)
        {
            var ghost = InstantiatePreview(prefabName);

            // 非表示の複数Colliderも本番の無効化経路へ通す
            // Pass multiple dormant colliders through the production disabling path
            var child = new GameObject("InactiveColliderChild");
            child.transform.SetParent(ghost.transform, false);
            child.AddComponent<BoxCollider>();
            child.AddComponent<SphereCollider>();
            child.SetActive(false);
            PreviewBlockCreator.DisableColliders(ghost);

            // 子の有効化とプール再利用でも当たり判定は復活しない
            // Activating children and reusing the pool must not restore collisions
            child.SetActive(true);
            ghost.SetActive(false);
            ghost.SetActive(true);
            foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(collider.enabled, collider.name);
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
            PreviewBlockCreator.DisableColliders(ghost);

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
