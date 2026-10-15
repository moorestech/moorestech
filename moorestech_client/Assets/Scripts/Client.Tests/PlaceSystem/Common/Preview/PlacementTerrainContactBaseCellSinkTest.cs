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
    // 床で沈む最下段セルは埋まり判定にせず、上部セルへの食い込みだけを判定する(ADR 0080)
    // Ignore the floor-sunk base cell and flag only intrusion into upper cells (ADR 0080).
    public class PlacementTerrainContactBaseCellSinkTest
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

        // 歯車チェーンポール3x4x3・コンパクト1x2x1・レール橋脚3x1x3・ベルト1x1x1
        // Gear chain pole 3x4x3, compact pole 1x2x1, rail pier 3x1x3, belt 1x1x1.
        [TestCase(3, 4, 3, 0.3f)]
        [TestCase(3, 4, 3, 0.7f)]
        [TestCase(1, 2, 1, 0.3f)]
        [TestCase(1, 2, 1, 0.7f)]
        [TestCase(3, 1, 3, 0.3f)]
        [TestCase(3, 1, 3, 0.7f)]
        [TestCase(1, 1, 1, 0.3f)]
        [TestCase(1, 1, 1, 0.7f)]
        public void FractionalFlatGroundInsideBaseCellIsNotBuried(int x, int y, int z, float surfaceHeight)
        {
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateSlopedGround(size, surfaceHeight, 0f);
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 4, 3)]
        [TestCase(1, 2, 1)]
        [TestCase(3, 1, 3)]
        [TestCase(1, 1, 1)]
        public void GroundRisingHalfMeterAcrossThreeCellsIsNotBuried(int x, int y, int z)
        {
            // 3セルで0.5m上がる斜面を、フットプリント中央の地表が0.45mになるよう置く
            // Place a 0.5m-per-3-cells slope whose surface is 0.45m at the footprint center.
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateSlopedGround(size, 0.45f, Mathf.Atan2(0.5f, 3f) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 4, 3, 2.0f)]
        [TestCase(1, 2, 1, 1.5f)]
        public void CliffIntoUpperCellsIsBuried(int x, int y, int z, float cliffTop)
        {
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateCliff(size, cliffTop);
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 1, 3)]
        [TestCase(1, 1, 1)]
        public void HeightOneBlockHasNoContactVolumeEvenInsideCliff(int x, int y, int z)
        {
            // 高さ1は#1499前と同じく地形ゲートを持たない
            // Height-1 blocks have no terrain gate, matching pre-#1499 behavior.
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateCliff(size, 1.5f);
            Physics.SyncTransforms();
            Assert.IsEmpty(preview.GetComponentsInChildren<GroundCollisionDetector>(true));
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        private BlockPreviewObject CreatePreview(Vector3Int size)
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.SetTerrainContactEnabled(true, size);
            return preview;
        }

        private void CreateSlopedGround(Vector3Int size, float surfaceHeightAtCenter, float slopeDegrees)
        {
            // 厚い箱をX方向へ傾け、上面がフットプリント中央を指定高さで通るよう置く
            // Tilt a thick box along X so its top passes the footprint center at the given height.
            var rotation = Quaternion.Euler(0f, 0f, slopeDegrees);
            var surfaceCenter = new Vector3(size.x * 0.5f, surfaceHeightAtCenter, size.z * 0.5f);
            var box = CreateGroundBox(new Vector3(size.x + 10f, 10f, size.z + 10f));
            box.transform.SetPositionAndRotation(surfaceCenter - rotation * new Vector3(0f, 5f, 0f), rotation);
        }

        private void CreateCliff(Vector3Int size, float cliffTop)
        {
            // フットプリントの+X端1列だけを、上面cliffTop・下面-5で覆う
            // Cover only the +X edge column of the footprint, from -5 up to cliffTop.
            var box = CreateGroundBox(new Vector3(1f, cliffTop + 5f, size.z + 2f));
            box.transform.position = new Vector3(size.x - 0.5f, (cliffTop - 5f) * 0.5f, size.z * 0.5f);
        }

        private BoxCollider CreateGroundBox(Vector3 boxSize)
        {
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            var box = ground.AddComponent<BoxCollider>();
            box.size = boxSize;
            return box;
        }
    }
}
#endif
