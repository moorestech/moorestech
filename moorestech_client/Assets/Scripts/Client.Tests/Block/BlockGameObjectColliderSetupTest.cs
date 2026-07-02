using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Block
{
    /// <summary>
    /// ブロック生成時のColliderセットアップとフォールバック条件のテスト
    /// Tests for block collider setup and the zero-collider fallback condition
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
        public void AuthoredColliderExists_NoMeshColliderFallback()
        {
            // 実体Colliderを持つブロックはフォールバックせず、Collider持ちの子にのみChildが付く
            // A block with a real collider gets no fallback; only collider children get a child component
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
            // Preview-only colliders don't count as real; fallback triggers but skips the preview subtree
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
        public void DisabledColliderOnly_FallbackTriggers()
        {
            // 無効化されたColliderしか無いブロックはフォールバック対象になる
            // A block whose only collider is disabled still triggers the fallback
            var blockObj = CreateBlockRoot();
            var disabledColliderChild = CreateChild(blockObj, "DisabledColliderChild");
            var boxCollider = disabledColliderChild.AddComponent<BoxCollider>();
            boxCollider.enabled = false;
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

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
            var child = new GameObject(childName);
            child.transform.SetParent(blockObj.transform);
            return child;
        }
    }
}
