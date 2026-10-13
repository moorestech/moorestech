#if UNITY_EDITOR
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
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
        private PhysicsScene _physics;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _physics = _scene.GetPhysicsScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _physics);
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

        [TestCase("Assets/AddressableResources/Block/metal_gear belt conveyor.prefab")]
        [TestCase("Assets/AddressableResources/Block/metal_gear belt conveyor splitter.prefab")]
        [TestCase("Assets/AddressableResources/Block/Old/wooden chest.prefab")]
        public void RealPrefabDoesNotBlockGroundAcrossHideAndReuse(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, path);
            var sourceLayers = asset.GetComponentsInChildren<Collider>(true).Select(c => c.gameObject.layer).ToArray();
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            ghost.SetActive(true);
            _physics.Simulate(0.02f);
            var cube = ghost.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.name == "Cube_4");
            var center = cube == null ? new Vector3(0.5f, 0.5f, 0.5f) : cube.bounds.center;
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.transform.position = new Vector3(center.x, -1f, center.z);
            ground.AddComponent<GroundGameObject>();
            ground.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);

            // 実アセットの照準経路を表示とプール再利用にまたがり検査する
            // Check the real asset's aim ray across visibility and pool reuse.
            var preview = ghost.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();
            var origin = new Vector3(center.x, 10f, center.z);
            for (var frame = 0; frame < 60; frame++)
            {
                preview.SetActive(frame % 3 != 1);
                _physics.Simulate(0.02f);
                Assert.IsTrue(_physics.Raycast(origin, Vector3.down, out var hit, 100f,
                    LayerConst.Without_Player_MapObject_Block_LayerMask, QueryTriggerInteraction.Collide));
                Assert.AreSame(ground, hit.collider.gameObject, $"{path}: frame={frame}");
            }
            CollectionAssert.AreEqual(sourceLayers,
                asset.GetComponentsInChildren<Collider>(true).Select(c => c.gameObject.layer).ToArray());
        }

        [Test]
        public void InactiveNestedAndMultipleCollidersRemainTriggersAfterActivation()
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var child = new GameObject("InactiveDefaultChild");
            child.transform.SetParent(root.transform);
            child.AddComponent<BoxCollider>();
            child.AddComponent<SphereCollider>();
            child.SetActive(false);
            root.AddComponent<BlockPreviewObject>().ConfigurePlacementColliders();
            child.SetActive(true);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsTrue(collider.enabled);
                Assert.IsTrue(collider.isTrigger);
                Assert.AreEqual(LayerConst.BlockLayer, collider.gameObject.layer);
                Assert.AreEqual(0, LayerConst.Without_Player_MapObject_Block_LayerMask & (1 << collider.gameObject.layer));
            }
        }

        [Test]
        public void MissingDetectorGetsOneInsetContactVolumeWithoutChangingVisualCollider()
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var visual = new GameObject("VisualOverhang");
            visual.transform.SetParent(root.transform, false);
            var visualCollider = visual.AddComponent<BoxCollider>();
            visualCollider.center = new Vector3(0.5f, 0.4f, 0.5f);
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();

            // 同一プール個体を再設定しても専用接触源を増やさない
            // Pool reuse must not add a second contact source.
            preview.ConfigureTerrainContact(new Vector3Int(1, 1, 1));
            preview.ConfigureTerrainContact(new Vector3Int(1, 1, 1));

            var detector = root.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            var box = detector.GetComponent<BoxCollider>();
            var body = root.GetComponent<Rigidbody>();
            var inset = Physics.defaultContactOffset * 2f;
            Assert.IsTrue(body.isKinematic);
            Assert.IsFalse(body.useGravity);
            Assert.IsTrue(box.isTrigger);
            Assert.AreEqual(LayerConst.BlockLayer, box.gameObject.layer);
            Assert.AreEqual(inset, box.bounds.min.y, 0.0001f);
            Assert.AreEqual(1f - inset, box.bounds.max.y, 0.0001f);
            Assert.IsTrue(visualCollider.enabled);
            Assert.IsTrue(visualCollider.isTrigger);
            Assert.AreEqual(LayerConst.BlockLayer, visualCollider.gameObject.layer);
        }

        [Test]
        public void ExistingDetectorRemainsTheOnlyContactSource()
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var child = new GameObject("ExistingContact");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<BoxCollider>();
            var existing = child.AddComponent<GroundCollisionDetector>();
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();

            preview.ConfigureTerrainContact(new Vector3Int(1, 1, 1));

            Assert.AreSame(existing, root.GetComponentsInChildren<GroundCollisionDetector>(true).Single());
            Assert.IsNull(root.GetComponent<Rigidbody>());
            Assert.IsFalse(root.transform.Cast<Transform>().Any(t => t.name == "PlacementGroundContactVolume"));
        }
    }
}
#endif
