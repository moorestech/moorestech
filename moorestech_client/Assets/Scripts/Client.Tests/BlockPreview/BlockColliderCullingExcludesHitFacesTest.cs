using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BlockColliderCullingExcludesHitFacesTest
    {
        private GameObject _storeObject;
        private GameObject _blockObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_blockObject);
            Object.DestroyImmediate(_storeObject);
        }

        [Test]
        public void OutOfRange_KeepsHitFacesEnabled_AndDisablesBlockColliders()
        {
            var storeObject = _storeObject = new GameObject("culling-test-store");
            var blockObject = _blockObject = new GameObject("culling-test-block");
            var block = blockObject.AddComponent<BlockGameObject>();
            var bodyCollider = blockObject.AddComponent<BoxCollider>();
            var hitFaces = BlockPlacementHitFaces.Create(blockObject.transform, Vector3Int.one, BlockDirection.North);
            var service = new BlockColliderCullingRegisterService(storeObject.AddComponent<BlockGameObjectDataStore>(), new ColliderDistanceCullingManager(null));

            // 範囲外にすると本体のコライダーだけが切れる
            // Going out of range disables only the block body collider
            service.CreateEntry(block).Target.SetWithinRange(false);
            Assert.IsFalse(bodyCollider.enabled);
            var faceColliders = hitFaces.GetComponents<BoxCollider>();
            Assert.AreEqual(6, faceColliders.Length);
            foreach (var faceCollider in faceColliders) Assert.IsTrue(faceCollider.enabled);
        }
    }
}
