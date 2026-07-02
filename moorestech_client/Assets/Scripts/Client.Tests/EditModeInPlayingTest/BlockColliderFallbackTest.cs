using System.Collections;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// Collider皆無プレハブへのフォールバック付与と、既存Colliderプレハブの非フォールバックをRaycastで実機検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies via raycast that the fallback attaches colliders to collider-less prefabs and doesn't fire for authored ones.
    /// </summary>
    public class BlockColliderFallbackTest
    {
        // Fast_BeltConveyor_Straightは自前Collider無し、stone crasherはBlockレイヤーのCapsuleCollider持ち
        // Fast_BeltConveyor_Straight has no authored collider; stone crasher has a Block-layer CapsuleCollider
        private const string NoColliderBlockName = "直進高速ベルトコンベア";
        private const string AuthoredColliderBlockName = "石の粉砕機";

        [UnityTest]
        public IEnumerator ColliderlessBlock_GetsFallbackCollider_AuthoredBlock_DoesNot()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // Collider皆無ブロックはフォールバックでMeshColliderが付き、クリックRaycastが通ることを確認
                // The collider-less block gains fallback MeshColliders and becomes hittable by the click raycast
                PlaceBlock(NoColliderBlockName, new Vector3Int(0, 0, 0), BlockDirection.North);
                var noColliderBlock = await WaitBlockGameObjectSpawn(new Vector3Int(0, 0, 0));
                var fallbackColliders = GetClickableColliders(noColliderBlock);
                Assert.IsNotEmpty(fallbackColliders, "collider-less block has no clickable collider (fallback did not fire)");
                Assert.IsTrue(fallbackColliders.All(c => c is MeshCollider), "fallback colliders should be MeshColliders");
                AssertClickRaycastResolvesBlock(noColliderBlock, fallbackColliders);

                // 既存Collider持ちブロックはフォールバックせず（MeshCollider無し）、Raycastが通ることを確認
                // The authored-collider block doesn't fall back (no MeshCollider) and is hittable by the raycast
                PlaceBlock(AuthoredColliderBlockName, new Vector3Int(10, 0, 10), BlockDirection.North);
                var authoredBlock = await WaitBlockGameObjectSpawn(new Vector3Int(10, 0, 10));
                var authoredColliders = GetClickableColliders(authoredBlock);
                Assert.IsNotEmpty(authoredColliders, "authored-collider block has no clickable collider");
                Assert.IsTrue(authoredColliders.All(c => c is not MeshCollider), "authored block should not gain fallback MeshColliders");
                AssertClickRaycastResolvesBlock(authoredBlock, authoredColliders);
            }

            Collider[] GetClickableColliders(BlockGameObject blockGameObject)
            {
                // 本番と同じ判定（BlockGameObjectColliderSetup.IsClickableCollider）でクリック可能Colliderを抽出
                // Extract clickable colliders using the same production predicate
                return blockGameObject.GetComponentsInChildren<Collider>()
                    .Where(c => BlockGameObjectColliderSetup.IsClickableCollider(blockGameObject.transform, c))
                    .ToArray();
            }

            void AssertClickRaycastResolvesBlock(BlockGameObject blockGameObject, Collider[] clickableColliders)
            {
                // クリック処理と同じレイヤーマスクで真上からRaycastし、対象ブロックへ解決できることを確認
                // Raycast downward with the click layer mask and verify it resolves to the target block
                var bounds = clickableColliders[0].bounds;
                foreach (var clickableCollider in clickableColliders) bounds.Encapsulate(clickableCollider.bounds);

                var origin = bounds.center + Vector3.up * 10f;
                var isHit = Physics.Raycast(origin, Vector3.down, out var hit, 50f, LayerConst.BlockOnlyLayerMask);
                Assert.IsTrue(isHit, $"click raycast did not hit any Block-layer collider: {blockGameObject.name}");

                var child = hit.collider.gameObject.GetComponentInChildren<BlockGameObjectChild>();
                Assert.IsNotNull(child, $"hit collider has no BlockGameObjectChild: {hit.collider.name}");
                Assert.AreEqual(blockGameObject, child.BlockGameObject, "raycast hit resolved to a different block");
            }

            #endregion
        }
    }
}
