using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BlockPlacementHitFacesTest
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        public void OutsideRays_ResolveWorldFaces(BlockDirection direction)
        {
            var bounds = CreateHitFaces(direction);

            // 外から各ワールド面へ撃つと、その面の種類が返る
            // A ray from outside toward each world face returns that face type
            Assert.AreEqual(PreviewSurfaceType.YZ_X, CastFrom(bounds.center + Vector3.right * 20, Vector3.left));
            Assert.AreEqual(PreviewSurfaceType.YZ_Origin, CastFrom(bounds.center + Vector3.left * 20, Vector3.right));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, CastFrom(bounds.center + Vector3.up * 20, Vector3.down));
            Assert.AreEqual(PreviewSurfaceType.YX_Z, CastFrom(bounds.center + Vector3.forward * 20, Vector3.back));
        }

        [Test]
        public void InsideRay_HitsFarFaceFromInside()
        {
            // 占有範囲の内側から撃っても、奥の面に当たる（今の枠と同じ）
            // A ray starting inside the footprint still hits the far face, as the old frame did
            var bounds = CreateHitFaces(BlockDirection.North);
            Assert.AreEqual(PreviewSurfaceType.YZ_X, CastFrom(bounds.center, Vector3.right));
        }

        [Test]
        public void Create_MakesOneObjectWithSixTriggerFaces()
        {
            CreateHitFaces(BlockDirection.North);
            var hitFaces = _root.GetComponentInChildren<BlockPlacementHitFaces>();
            Assert.AreEqual(1, _root.GetComponentsInChildren<BlockPlacementHitFaces>().Length);
            var colliders = hitFaces.GetComponents<BoxCollider>();
            Assert.AreEqual(6, colliders.Length);
            foreach (var collider in colliders) Assert.IsTrue(collider.isTrigger);
            Assert.AreEqual(Client.Common.LayerConst.BlockBoundingBoxLayer, hitFaces.gameObject.layer);
        }

        private Bounds CreateHitFaces(BlockDirection direction)
        {
            _root = new GameObject("hit-faces-test-block");
            _root.transform.SetPositionAndRotation(new Vector3(1000, 50, 1000), direction.GetRotation());
            var hitFaces = BlockPlacementHitFaces.Create(_root.transform, new Vector3Int(2, 1, 3), direction);
            Physics.SyncTransforms();
            var colliders = hitFaces.GetComponents<BoxCollider>();
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
            return bounds;
        }

        private static PreviewSurfaceType CastFrom(Vector3 origin, Vector3 direction)
        {
            Assert.IsTrue(Physics.Raycast(origin, direction, out var hit, 100f, Client.Common.LayerConst.BlockBoundingBoxOnlyLayerMask), $"no hit from {origin} toward {direction}");
            return hit.collider.GetComponent<BlockPlacementHitFaces>().GetSurfaceType(hit.collider);
        }
    }
}
