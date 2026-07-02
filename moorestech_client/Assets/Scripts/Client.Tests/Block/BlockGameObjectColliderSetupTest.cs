using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Block
{
    /// <summary>
    /// ブロック生成時のBlockGameObjectChild付与と、クリック可能Collider判定・欠落検出のテスト
    /// Tests for BlockGameObjectChild attachment, the clickable-collider predicate, and missing-collider detection
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
        public void SetupColliders_AttachesChildToAllCollidersIncludingInactive()
        {
            // 非アクティブ含む全Collider持ちの子にChildが付き、二重付与されない
            // All collider children incl. inactive get a child component, without double-attaching
            var blockObj = CreateBlockRoot();
            var colliderChild = CreateChild(blockObj, "ColliderChild");
            colliderChild.AddComponent<BoxCollider>();
            colliderChild.AddComponent<SphereCollider>();
            var inactiveColliderChild = CreateChild(blockObj, "InactiveColliderChild");
            inactiveColliderChild.AddComponent<BoxCollider>();
            inactiveColliderChild.SetActive(false);

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.AreEqual(1, colliderChild.GetComponents<BlockGameObjectChild>().Length);
            Assert.AreEqual(1, inactiveColliderChild.GetComponents<BlockGameObjectChild>().Length);
        }

        [Test]
        public void SetupColliders_NeverAddsColliders_AndReportsMissingClickable()
        {
            // 実行時にColliderを追加せず、クリック可能Collider欠落はエラーログで報告する
            // Never adds colliders at runtime; missing clickable colliders are reported as an error
            LogAssert.Expect(LogType.Error, new Regex("クリック可能なColliderがありません"));
            var blockObj = CreateBlockRoot();
            var rendererChild = CreateChild(blockObj, "RendererChild");
            rendererChild.AddComponent<MeshRenderer>();

            BlockGameObjectColliderSetup.SetupColliders(blockObj);

            Assert.AreEqual(0, blockObj.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void IsClickableCollider_BlockLayerEnabledCollider_IsClickable()
        {
            var blockObj = CreateBlockRoot();
            var collider = CreateChild(blockObj, "Collider").AddComponent<BoxCollider>();

            Assert.IsTrue(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, collider));
        }

        [Test]
        public void IsClickableCollider_DisabledCollider_IsNotClickable()
        {
            var blockObj = CreateBlockRoot();
            var collider = CreateChild(blockObj, "Collider").AddComponent<BoxCollider>();
            collider.enabled = false;

            Assert.IsFalse(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, collider));
        }

        [Test]
        public void IsClickableCollider_NonBlockLayerCollider_IsNotClickable()
        {
            // クリックRaycastはBlockレイヤーのみ対象のため、Defaultレイヤーは不可
            // The click raycast only targets the Block layer, so Default-layer colliders don't count
            var blockObj = CreateBlockRoot();
            var colliderChild = CreateChild(blockObj, "Collider");
            colliderChild.layer = 0;
            var collider = colliderChild.AddComponent<BoxCollider>();

            Assert.IsFalse(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, collider));
        }

        [Test]
        public void IsClickableCollider_GroundCollisionDetectorCollider_IsNotClickable()
        {
            // 設置判定専用Colliderはクリック当たりとして数えない
            // Placement-check-only colliders don't count as click targets
            var blockObj = CreateBlockRoot();
            var colliderChild = CreateChild(blockObj, "GroundCheck");
            colliderChild.AddComponent<GroundCollisionDetector>();
            var collider = colliderChild.AddComponent<BoxCollider>();

            Assert.IsFalse(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, collider));
        }

        [Test]
        public void IsClickableCollider_PreviewOnlyOrInactiveSubtree_IsNotClickable()
        {
            // PreviewOnly配下と非アクティブ祖先配下は実体Colliderとして扱わない
            // Colliders under preview-only or inactive subtrees don't count as real
            var blockObj = CreateBlockRoot();
            var previewChild = CreateChild(blockObj, "Preview");
            previewChild.AddComponent<PreviewOnlyObject>();
            var previewCollider = previewChild.AddComponent<BoxCollider>();
            var inactiveParent = CreateChild(blockObj, "InactiveParent");
            var nestedCollider = CreateChild(blockObj, "Nested").AddComponent<BoxCollider>();
            nestedCollider.transform.SetParent(inactiveParent.transform);
            inactiveParent.SetActive(false);

            Assert.IsFalse(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, previewCollider));
            Assert.IsFalse(BlockGameObjectColliderSetup.IsClickableCollider(blockObj.transform, nestedCollider));
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
