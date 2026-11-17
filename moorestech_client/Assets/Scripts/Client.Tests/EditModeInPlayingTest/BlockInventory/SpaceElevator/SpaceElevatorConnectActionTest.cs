using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Tests.EditModeInPlayingTest.Util.World;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using static Client.Tests.EditModeInPlayingTest.Ui.UiStateTransitionWait;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    [Category("CiShardClientPlay2")]
    public class SpaceElevatorConnectActionTest
    {
        [UnityTest]
        public IEnumerator RegisteredActionUsesOpenCoordinatesAndReturnsAllThreeResults()
        {
            EnterPlayModeUtil();
            // PlayMode遷移はUnityTest直下に置く
            // Keep the PlayMode transition directly inside the UnityTest
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
                var directory = SpaceElevatorActionTestMod.Create();
                await LoadMainGame(directory, null);

                // 開いた画面は届く範囲を外れると閉じるので、足場を置く前に自機を対象の -x の隣へ移す
                // The opened screen closes once out of reach, so move the player next to the target on -x before placing the ground
                var position = new Vector3Int(20, 0, 4);
                PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(position + new Vector3(-1.5f, 0.1f, 0.5f));
                var ground = TestPlayerGround.Create();

                // 実足場を置き例外を失敗扱い
                // Place real ground and fail on exceptions
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                await TestPlayerGround.AssertGroundedAsync();
                var master = MasterHolder.BlockMaster.GetBlockAllIds().Select(id => MasterHolder.BlockMaster.GetBlockMaster(id))
                    .Single(b => b.BlockParam is SpaceElevatorBaseBlockParam);
                var target = await ServerBlockPlacement.PlaceAsync(master.Name, position, BlockDirection.North);
                var decoy = await ServerBlockPlacement.PlaceAsync(master.Name, Vector3Int.zero, BlockDirection.North);
                var view = await WaitBlockGameObjectSpawn(position);
                var colliders = view.GetComponentsInChildren<Collider>(true);
                foreach (var candidate in colliders)
                    Debug.Log($"[SpaceElevatorCollider] name={candidate.name} type={candidate.GetType().Name} layer={candidate.gameObject.layer} enabled={candidate.enabled} active={candidate.gameObject.activeInHierarchy} clickable={BlockGameObjectColliderSetup.IsClickableCollider(view.transform, candidate)} parent={candidate.transform.parent.name}");
                // 実配置でクリック・所有者・操作を検証
                // Verify clicking, ownership and interaction on the placed block
                var collider = colliders.Where(c => BlockGameObjectColliderSetup.IsClickableCollider(view.transform, c)).Single();
                Assert.AreEqual(LayerConst.BlockLayer, collider.gameObject.layer);
                Assert.IsTrue(BlockGameObjectColliderSetup.IsClickableCollider(view.transform, collider));
                Physics.SyncTransforms();
                var ray = new Ray(collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 2), Vector3.down);
                Assert.IsTrue(Physics.Raycast(ray, out var hit, 10, LayerConst.BlockOnlyLayerMask));
                Assert.AreSame(collider, hit.collider);
                var child = hit.collider.GetComponent<BlockGameObjectChild>();
                Assert.AreSame(view, child.BlockGameObject);
                Assert.AreSame(view.Interactable, child.Interactable);
                Assert.IsNotEmpty(view.Interactable.Actions);
                Debug.Log($"[SpaceElevatorClick] total={colliders.Length} clickable=1 layer={collider.gameObject.layer} owner={child.BlockGameObject.name} actions={view.Interactable.Actions.Count}");
                // UITK のパネルは画面が SubInventory のときだけ開くので、先に遷移してから実プレイと同じ文脈で開く（前例 GameUiMachineSectionWiringTest。文脈なしのエラーログが 1 回出る）
                // The UITK panel opens only on the SubInventory screen, so transition first and reopen with the real-play context (precedent GameUiMachineSectionWiringTest; one no-context error log)
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
                await TransitionTo(uiStateControl, UIStateEnum.SubInventory);
                var subInventory = await BlockSubInventoryOpener.Open(view);

                // 特殊族のコマンド・応答を通す
                // Exercise the special-family command and response
                var commands = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ISpecialBlockCommands>();
                var insufficient = await commands.ConnectSpaceElevator();
                AssertRejected(insufficient, UiCommandRejectReason.SpecialBlockRequestFailed, "insufficient_cable");
                var param = (SpaceElevatorBaseBlockParam)master.BlockParam;
                var insufficientState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.AreEqual(0, insufficientState.PoolAmount);
                Assert.IsFalse(insufficientState.TargetConnected);
                var supplied = await SpaceElevatorServerTestState.SupplyCableAsync(param);
                var connected = await commands.ConnectSpaceElevator();
                Assert.IsInstanceOf<UiCommandAccepted>(connected, (connected as UiCommandRejected)?.ToLogText());
                var connectedState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.IsTrue(connectedState.TargetConnected);
                Assert.IsFalse(connectedState.DecoyConnected, "request used the decoy coordinates");
                Assert.AreEqual(supplied - param.CablesPerElevator, connectedState.PoolAmount);
                var already = await commands.ConnectSpaceElevator();
                AssertRejected(already, UiCommandRejectReason.SpecialBlockCommandStale, "already_connected");
                var alreadyState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.AreEqual(connectedState.PoolAmount, alreadyState.PoolAmount);
                BlockSubInventoryOpener.Close(subInventory);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);

                // 正規撤去の返却を検証する
                // Verify refunds through the normal removal request
                var removed = await ClientContext.VanillaApi.Response.Block.BlockRemove(position, CancellationToken.None);
                Assert.IsTrue(removed.Success);
                var refundedState = await SpaceElevatorServerTestState.ReadAsync(decoy, decoy);
                Assert.AreEqual(supplied, refundedState.PoolAmount);
                Assert.IsFalse(refundedState.DecoyConnected);
                Object.DestroyImmediate(ground);
                Directory.Delete(directory, true);
            }

            // 拒否の理由と、サーバーの結果を写した失敗コードを見る
            // Check the rejection reason and the failure code mapped from the server result
            void AssertRejected(UiCommandResult result, UiCommandRejectReason reason, string wireCode)
            {
                Assert.IsInstanceOf<UiCommandRejected>(result, $"expected {wireCode} but the command was accepted");
                var rejected = (UiCommandRejected)result;
                Assert.AreEqual(reason, rejected.Reason);
                Assert.AreEqual(wireCode, rejected.ToWireCode());
            }
            #endregion
        }
    }
}
