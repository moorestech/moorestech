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
                await WaitForBoundingBox(near);
                await WaitForBoundingBox(far);
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
                await WaitForBoundingBox(newNear);
                await WaitForBoundingBox(newFar);
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
                Assert.IsFalse(far.GetComponentInChildren<BlockPreviewBoundingBox>(true).gameObject.activeSelf);
                Object.Destroy(storeObject);
            }

            async UniTask WaitForBoundingBox(BlockGameObject block)
            {
                // 非同期ロードの失敗は有限待機後のアサートで検出する
                // Detect asynchronous loading failures with a bounded wait and assertion
                for (var frame = 0; frame < 300; frame++)
                {
                    if (block.GetComponentInChildren<BlockPreviewBoundingBox>(true) != null) return;
                    await UniTask.Yield();
                }
                Assert.Fail($"Bounding box did not load for {block.name}");
            }

            void AssertPreview(BlockGameObject block, bool rendered)
            {
                var box = block.GetComponentInChildren<BlockPreviewBoundingBox>(true);
                Assert.IsNotNull(box);
                Assert.IsTrue(box.gameObject.activeInHierarchy, "枠線と面は半径外でも有効");
                var renderers = box.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers);
                foreach (var renderer in renderers) Assert.AreEqual(rendered, renderer.enabled, renderer.name);

                // 描画抑止が隣接設置の面コライダーを失わせない
                // Rendering suppression must preserve face colliders used for adjacent placement
                var surfaces = box.GetComponentsInChildren<BlockPreviewBoundingBoxSurface>(true);
                Assert.IsNotEmpty(surfaces);
                foreach (var surface in surfaces)
                {
                    var collider = surface.GetComponent<Collider>();
                    Assert.IsNotNull(collider);
                    Assert.IsTrue(collider.enabled);
                    Assert.IsTrue(collider.gameObject.activeInHierarchy);
                }
            }

            #endregion
        }
    }
}
