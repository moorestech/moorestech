using System.Collections;
using System.Linq;
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
    /// Collider皆無プレハブへのフォールバック付与と、既存Colliderプレハブの非フォールバックを実機で検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies the fallback attaches colliders to collider-less prefabs and doesn't fire for authored ones.
    /// </summary>
    public class BlockColliderFallbackTest
    {
        // Fast_BeltConveyor_Straightは自前Collider無し、gear belt conveyorはBoxCollider1つ持ち
        // Fast_BeltConveyor_Straight has no authored collider; gear belt conveyor has one BoxCollider
        private const string NoColliderBlockName = "直進高速ベルトコンベア";
        private const string AuthoredColliderBlockName = "直線歯車ベルトコンベア";

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

                // Collider皆無ブロックを設置し、フォールバックでMeshColliderとChildが付くことを確認
                // Place the collider-less block and verify the fallback attaches a MeshCollider and child component
                var noColliderBlock = await PlaceAndWaitBlock(NoColliderBlockName, new Vector3Int(0, 0, 0));
                var fallbackColliders = GetEffectiveColliders(noColliderBlock);
                Assert.IsNotEmpty(fallbackColliders, "collider-less block has no runtime collider (fallback did not fire)");
                Assert.IsTrue(fallbackColliders.All(c => c is MeshCollider), "fallback colliders should be MeshColliders");
                Assert.IsTrue(fallbackColliders.All(c => c.TryGetComponent<BlockGameObjectChild>(out _)),
                    "fallback collider object lacks BlockGameObjectChild (click resolution would fail)");

                // 既存Collider持ちブロックはフォールバックせず、MeshColliderが増えないことを確認
                // Verify the authored-collider block doesn't trigger the fallback and gains no MeshCollider
                var authoredBlock = await PlaceAndWaitBlock(AuthoredColliderBlockName, new Vector3Int(5, 0, 0));
                var authoredColliders = GetEffectiveColliders(authoredBlock);
                Assert.IsNotEmpty(authoredColliders, "authored-collider block has no runtime collider");
                Assert.IsTrue(authoredColliders.All(c => c is BoxCollider), "authored block should only have its BoxCollider (no fallback MeshCollider)");
                Assert.IsTrue(authoredColliders.All(c => c.TryGetComponent<BlockGameObjectChild>(out _)),
                    "authored collider object lacks BlockGameObjectChild");
            }

            async UniTask<BlockGameObject> PlaceAndWaitBlock(string blockName, Vector3Int pos)
            {
                // サーバー側に設置し、クライアント側のBlockGameObjectがスポーンするまで待機
                // Place on the server and wait until the client-side BlockGameObject spawns
                PlaceBlock(blockName, pos, BlockDirection.North);
                BlockGameObject blockGameObject = null;
                for (var i = 0; i < 180 && blockGameObject == null; i++)
                {
                    ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out blockGameObject);
                    await UniTask.Yield();
                }
                Assert.IsNotNull(blockGameObject, $"client BlockGameObject not spawned: {blockName}");
                return blockGameObject;
            }

            Collider[] GetEffectiveColliders(BlockGameObject blockGameObject)
            {
                // アクティブかつ有効なColliderのみを実行時有効として数える（PreviewOnly配下は非アクティブ化済み）
                // Count only active+enabled colliders as runtime-effective (preview-only subtrees are already deactivated)
                return blockGameObject.GetComponentsInChildren<Collider>()
                    .Where(c => c.enabled)
                    .ToArray();
            }

            #endregion
        }
    }
}
