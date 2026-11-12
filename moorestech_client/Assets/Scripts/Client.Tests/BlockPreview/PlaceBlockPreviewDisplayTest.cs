using System.Collections;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState.State.PlaceBlock;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests
{
    [Category("CiShardClientPlay3")]
    public class PlaceBlockPreviewDisplayTest
    {
        [UnityTest]
        public IEnumerator RealPlacement_MovementAndAnimation_ControlPreview()
        {
            EnterPlayModeUtil();
            // 遷移はテスト直下で実行する
            // Enter play mode directly from the test enumerator
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();
                var storeObject = new GameObject("preview-placement-test-store");
                var store = storeObject.AddComponent<BlockGameObjectDataStore>();
                var player = PlayerSystemContainer.Instance;
                player.PlayerObjectController.SetPlayerPosition(Vector3.zero);
                var manager = new BlockPreviewRadiusCullingManager(player);
                var blockId = FindBlockIdByName("直進高速ベルトコンベア");

                // 本番サービスで既設登録と初回評価
                // Register existing blocks and evaluate through the real service
                store.PlaceBlock(Vector3Int.zero, blockId, BlockDirection.North, new BlockInstanceId(900001), false);
                var farPosition = new Vector3Int(40, 0, 0);
                store.PlaceBlock(farPosition, blockId, BlockDirection.North, new BlockInstanceId(900002), false);
                new BlockPreviewRadiusRegisterService(store, manager).Start();
                manager.Tick();
                var display = new PlaceBlockPreviewDisplay(store);
                display.OnEnter();
                var near = store.GetBlockGameObject(Vector3Int.zero);
                var far = store.GetBlockGameObject(farPosition);
                AssertPreview(near, true);
                AssertPreview(far, false);

                // 入場後の非アニメ設置通知で表示更新
                // Update display on nonanimated placement notifications after entry
                var newNearPosition = new Vector3Int(0, 0, 4);
                var newFarPosition = new Vector3Int(40, 0, 4);
                store.PlaceBlock(newNearPosition, blockId, BlockDirection.North, new BlockInstanceId(900003), false);
                store.PlaceBlock(newFarPosition, blockId, BlockDirection.North, new BlockInstanceId(900004), false);
                var newNear = store.GetBlockGameObject(newNearPosition);
                var newFar = store.GetBlockGameObject(newFarPosition);
                AssertPreview(newNear, true);
                AssertPreview(newFar, false);

                // 実プレイヤー移動で登録対象の内外を反転
                // Reverse registered targets’ range membership by moving the real player
                player.PlayerObjectController.SetPlayerPosition(new Vector3(40, 0, 0));
                manager.Tick();
                AssertPreview(near, false);
                AssertPreview(far, true);
                AssertPreview(newNear, false);
                AssertPreview(newFar, true);

                // 状態を書き換えず実アニメーションの開始・完了を通す
                // Exercise actual animation start and completion without assigning its state
                display.OnExit();
                var animation = far.PlayPlaceAnimation();
                display.OnEnter();
                Assert.IsTrue(far.IsPlayingPlaceAnimation);
                AssertPreview(far, false);
                display.OnExit();
                display.OnEnter();
                AssertPreview(far, false);
                await animation;
                Assert.IsFalse(far.IsPlayingPlaceAnimation);
                AssertPreview(far, true);
                display.OnExit();
                display.OnEnter();
                AssertPreview(far, true);
                display.OnExit();
                // 出ると有効な枠は0個。出入りをくり返しても総数は増え続けない
                // Leaving leaves no active frame, and repeated entry does not keep growing the total
                Assert.AreEqual(0, Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsSortMode.None).Length);
                var totalAfterFirstCycle = Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                display.OnEnter();
                display.OnExit();
                display.OnEnter();
                display.OnExit();
                Assert.AreEqual(totalAfterFirstCycle, Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                Object.Destroy(storeObject);
            }

            void AssertPreview(BlockGameObject block, bool rendered)
            {
                // 旧枠はブロック配下に無い
                // No old frame lives under the block
                Assert.IsNull(block.GetComponentInChildren<BlockPreviewBoundingBox>(true));

                // 描画抑止が隣接設置の面コライダーを失わせない
                // Rendering suppression must preserve face colliders used for adjacent placement
                var hitFaces = block.GetComponentInChildren<BlockPlacementHitFaces>(true);
                Assert.IsNotNull(hitFaces);
                Assert.IsTrue(hitFaces.gameObject.activeInHierarchy, "当たり判定は半径外でも有効");
                var colliders = hitFaces.GetComponents<BoxCollider>();
                Assert.AreEqual(6, colliders.Length);
                foreach (var collider in colliders) Assert.IsTrue(collider.enabled);

                // 半径内のブロック位置にだけ、プールの枠が有効で置かれる
                // Only blocks within the radius get an active pooled frame at their position
                Assert.AreEqual(rendered, ActiveFrameAt(block) != null, block.name);
            }

            BlockPreviewBoundingBox ActiveFrameAt(BlockGameObject block)
            {
                foreach (var frame in Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsSortMode.None))
                    if (frame.transform.position == block.transform.position) return frame;
                return null;
            }

            #endregion
        }
    }
}
