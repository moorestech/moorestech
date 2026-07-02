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
    /// 焼き込み済みClickColliderと手付けColliderの両方でクリックRaycastが通ることを実機検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies via raycast that both baked ClickColliders and hand-authored colliders are clickable in-game.
    /// </summary>
    public class BlockClickColliderTest
    {
        // Fast_BeltConveyor_StraightはBaker焼き込みのClickCollider、stone crasherは手付けCapsuleCollider
        // Fast_BeltConveyor_Straight has a baked ClickCollider; stone crasher has a hand-authored CapsuleCollider
        private const string BakedColliderBlockName = "直進高速ベルトコンベア";
        private const string AuthoredColliderBlockName = "石の粉砕機";

        [UnityTest]
        public IEnumerator BakedAndAuthoredColliderBlocks_AreClickable_WithoutRuntimeMeshColliders()
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

                // 焼き込みClickColliderのブロック：実行時MeshCollider追加なしでクリックRaycastが通る
                // Baked-ClickCollider block: clickable by raycast without runtime-added MeshColliders
                PlaceBlock(BakedColliderBlockName, new Vector3Int(0, 0, 0), BlockDirection.North);
                var bakedBlock = await WaitBlockGameObjectSpawn(new Vector3Int(0, 0, 0));
                AssertNoMeshCollider(bakedBlock);
                var bakedColliders = GetClickableColliders(bakedBlock);
                Assert.IsNotEmpty(bakedColliders, "baked block has no clickable collider");
                AssertClickRaycastResolvesBlock(bakedBlock, bakedColliders);

                // 手付けColliderのブロック：焼き込み対象外のままクリックRaycastが通る
                // Hand-authored block: stays unbaked and clickable by raycast
                PlaceBlock(AuthoredColliderBlockName, new Vector3Int(10, 0, 10), BlockDirection.North);
                var authoredBlock = await WaitBlockGameObjectSpawn(new Vector3Int(10, 0, 10));
                AssertNoMeshCollider(authoredBlock);
                var authoredColliders = GetClickableColliders(authoredBlock);
                Assert.IsNotEmpty(authoredColliders, "authored-collider block has no clickable collider");
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

            void AssertNoMeshCollider(BlockGameObject blockGameObject)
            {
                // 実行時MeshCollider自動付与が復活していないことを確認（9e1751462の最適化を維持）
                // Ensure runtime MeshCollider auto-attachment has not returned (preserves the 9e1751462 optimization)
                Assert.IsEmpty(blockGameObject.GetComponentsInChildren<MeshCollider>(true),
                    $"unexpected runtime MeshCollider on {blockGameObject.name}");
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
