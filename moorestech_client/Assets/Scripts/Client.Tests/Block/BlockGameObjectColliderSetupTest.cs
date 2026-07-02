using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Block
{
    /// <summary>
    /// ブロック生成時のColliderセットアップと「クリック可能Colliderゼロ」フォールバック条件のテスト
    /// Tests for block collider setup and the zero-clickable-collider fallback condition
    /// </summary>
    public class BlockGameObjectColliderSetupTest
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects) Object.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        [Test]
        public void BlockLayerColliderExists_NoFallback()
        {
            // Blockレイヤーの実体Colliderがあればフォールバックせず、Collider持ちの子にのみChildが付く
            // With a clickable collider present, no fallback fires; only collider children get a child component
            var blockObj = CreateBlockRoot();
            var colliderChild = CreateChild(blockObj, "ColliderChild");
            colliderChild.AddComponent<BoxCollider>();
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(colliderChild.GetComponent<BlockGameObjectChild>());
            Assert.IsNull(rendererChild.GetComponent<BlockGameObjectChild>());
            Assert.IsNull(rendererChild.GetComponent<MeshCollider>());
        }

        [Test]
        public void ZeroCollider_FallbackAddsMeshColliderToRenderers()
        {
            // Collider皆無のブロックは全MeshRendererにChildとMeshColliderが付く
            // A block with zero colliders gets a child component and MeshCollider on every MeshRenderer
            var blockObj = CreateBlockRoot();
            var rendererChildA = CreateChild(blockObj, "RendererChildA");
            rendererChildA.AddComponent<MeshRenderer>();
            var rendererChildB = CreateChild(blockObj, "RendererChildB");
            rendererChildB.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(rendererChildA.GetComponent<BlockGameObjectChild>());
            Assert.IsNotNull(rendererChildA.GetComponent<MeshCollider>());
            Assert.IsNotNull(rendererChildB.GetComponent<BlockGameObjectChild>());
            Assert.IsNotNull(rendererChildB.GetComponent<MeshCollider>());
        }

        [Test]
        public void PreviewOnlyColliderOnly_FallbackTriggersAndSkipsPreview()
        {
            // PreviewOnly配下のColliderは実体扱いされずフォールバックし、Preview配下には付与しない
            // Preview-only colliders don't count as real; fallback fires but skips the preview subtree
            var blockObj = CreateBlockRoot();
            var previewChild = CreateChild(blockObj, "PreviewChild");
            previewChild.AddComponent<PreviewOnlyObject>();
            previewChild.AddComponent<BoxCollider>();
            previewChild.AddComponent<MeshRenderer>();
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(rendererChild.GetComponent<MeshCollider>());
            Assert.IsNotNull(rendererChild.GetComponent<BlockGameObjectChild>());
            Assert.IsNull(previewChild.GetComponent<MeshCollider>());
        }

        [Test]
        public void DisabledColliderOnRendererObject_FallbackAddsMeshColliderToSameObject()
        {
            // 無効Colliderしか無いブロックはフォールバックし、同一オブジェクトにもMeshColliderが付く
            // A block whose only collider is disabled falls back; the same object also gains a MeshCollider
            var blockObj = CreateBlockRoot();
            var rendererChild = CreateChild(blockObj, "DisabledColliderRenderer");
            rendererChild.AddComponent<MeshRenderer>();
            var boxCollider = rendererChild.AddComponent<BoxCollider>();
            boxCollider.enabled = false;

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(rendererChild.GetComponent<MeshCollider>());
        }

        [Test]
        public void NonBlockLayerColliderOnly_FallbackTriggers()
        {
            // Blockレイヤー以外のColliderはクリックRaycastに当たらないためフォールバック対象になる
            // Colliders off the Block layer can't be hit by the click raycast, so the fallback fires
            var blockObj = CreateBlockRoot();
            var defaultLayerColliderChild = CreateChild(blockObj, "DefaultLayerColliderChild");
            defaultLayerColliderChild.layer = 0;
            defaultLayerColliderChild.AddComponent<BoxCollider>();
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(rendererChild.GetComponent<MeshCollider>());
        }

        [Test]
        public void GroundCollisionDetectorColliderOnly_FallbackTriggers()
        {
            // 設置判定用Colliderのみのブロックはフォールバック対象になる
            // A block whose only collider is the placement-check collider falls back
            var blockObj = CreateBlockRoot();
            var groundCheckChild = CreateChild(blockObj, "GroundCheckChild");
            groundCheckChild.AddComponent<BoxCollider>();
            groundCheckChild.AddComponent<GroundCollisionDetector>();
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(rendererChild.GetComponent<MeshCollider>());
        }

        [Test]
        public void InactiveColliderOnly_ChildAttachedAndFallbackTriggers()
        {
            // 非アクティブ子のColliderにもChildが付き、クリック可能ゼロ扱いでフォールバックする
            // Inactive colliders still get a child component but don't count as clickable, so the fallback fires
            var blockObj = CreateBlockRoot();
            var inactiveColliderChild = CreateChild(blockObj, "InactiveColliderChild");
            inactiveColliderChild.AddComponent<BoxCollider>();
            inactiveColliderChild.SetActive(false);
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.IsNotNull(inactiveColliderChild.GetComponent<BlockGameObjectChild>());
            Assert.IsNotNull(rendererChild.GetComponent<MeshCollider>());
        }

        private BlockGameObject CreateBlockRoot()
        {
            var root = new GameObject("BlockRoot");
            _createdObjects.Add(root);
            return root.AddComponent<BlockGameObject>();
        }

        private GameObject CreateChild(BlockGameObject blockObj, string childName)
        {
            var child = new GameObject(childName)
            {
                layer = LayerConst.BlockLayer,
            };
            child.transform.SetParent(blockObj.transform);
            return child;
        }
    }
}
