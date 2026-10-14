#if UNITY_EDITOR
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PlacementTerrainContactShapeTest
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _scene.GetPhysicsScene());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void ExistingNonBoxDetectorUsesItsOwnShapeForCurrentPose()
        {
            var root = new GameObject("ExistingSpherePreview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.25f;
            root.AddComponent<GroundCollisionDetector>();
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();
            preview.SetTerrainContactEnabled(true, Vector3Int.one);
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(0.1f, 0.1f, 0.1f);
            ground.transform.position = new Vector3(0.23f, 0.23f, 0f);
            Physics.SyncTransforms();
            Assert.IsTrue(sphere.bounds.Intersects(box.bounds), "broad-phase candidate must exist");
            Assert.IsFalse(preview.DetectCurrentGroundOverlap(), "AABB intersection is not sphere penetration");
            ground.transform.position = new Vector3(0.2f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap());
        }

        [Test]
        public void CachedInactiveChildAndMultipleCollidersUseTheirLiveFlags()
        {
            var root = new GameObject("ExistingDetectorPreview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var child = new GameObject("DormantContact");
            child.transform.SetParent(root.transform, false);
            var box = child.AddComponent<BoxCollider>();
            var sphere = child.AddComponent<SphereCollider>();
            sphere.radius = 0.2f;
            child.AddComponent<GroundCollisionDetector>();
            box.enabled = false;
            child.SetActive(false);
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();
            preview.SetTerrainContactEnabled(true, Vector3Int.one);
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            ground.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap(), "inactive source is ignored");
            child.SetActive(true);
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap(), "cached sphere activates later");
            sphere.enabled = false;
            box.enabled = true;
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap(), "second cached collider becomes active");
            box.enabled = false;
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap(), "disabled sources do not count");
        }
    }
}
#endif
