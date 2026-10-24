using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    [Category("CiShardClientPlay2")]
    public class SpaceElevatorConnectActionTest
    {
        [UnityTest]
        public IEnumerator RegisteredActionUsesOpenCoordinatesAndReturnsAllThreeResults()
        {
            EnterPlayModeUtil();
            BlockActionSceneIsolation.Prepare();
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
                BlockActionSceneIsolation.AssertBrowserIsInactive();
                // 起動後の例外をテスト失敗として扱う
                // Fail the test on exceptions after startup
                LogAssert.ignoreFailingMessages = false;
                var master = MasterHolder.BlockMaster.GetBlockAllIds().Select(id => MasterHolder.BlockMaster.GetBlockMaster(id))
                    .Single(b => b.BlockParam is SpaceElevatorBaseBlockParam);
                var position = new Vector3Int(20, 0, 4);
                var target = await ServerBlockPlacement.PlaceAsync(master.Name, position, BlockDirection.North);
                var decoy = await ServerBlockPlacement.PlaceAsync(master.Name, Vector3Int.zero, BlockDirection.North);
                var view = await WaitBlockGameObjectSpawn(position);
                // 遅延ロードされるプレビューも検査対象へ含める
                // Include the asynchronously loaded preview in the inspected hierarchy
                for (var frame = 0; frame < 180 && view.GetComponentInChildren<BlockPreviewBoundingBox>(true) == null; frame++)
                    await UniTask.Yield();
                Assert.IsNotNull(view.GetComponentInChildren<BlockPreviewBoundingBox>(true));
                var colliders = view.GetComponentsInChildren<Collider>(true);
                Assert.Greater(colliders.Length, 1, "the delayed preview colliders must participate in this regression test");
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
                var subInventory = await BlockSubInventoryOpener.Open(view);

                // 実Binder・API・応答を通す
                // Exercise the real binder, API and server response
                var insufficient = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsFalse(insufficient.Ok);
                Assert.AreEqual("insufficient_cable", insufficient.Error);
                var param = (SpaceElevatorBaseBlockParam)master.BlockParam;
                var insufficientState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.AreEqual(0, insufficientState.PoolAmount);
                Assert.IsFalse(insufficientState.TargetConnected);
                var supplied = await SpaceElevatorServerTestState.SupplyCableAsync(param);
                var connected = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsTrue(connected.Ok, connected.Error);
                var connectedState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.IsTrue(connectedState.TargetConnected);
                Assert.IsFalse(connectedState.DecoyConnected, "request used the decoy coordinates");
                Assert.AreEqual(supplied - param.CablesPerElevator, connectedState.PoolAmount);
                var already = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsFalse(already.Ok);
                Assert.AreEqual("already_connected", already.Error);
                var alreadyState = await SpaceElevatorServerTestState.ReadAsync(target, decoy);
                Assert.AreEqual(connectedState.PoolAmount, alreadyState.PoolAmount);
                BlockSubInventoryOpener.Close(subInventory);

                // 正規撤去の返却を検証する
                // Verify refunds through the normal removal request
                var removed = await ClientContext.VanillaApi.Response.Block.BlockRemove(position, CancellationToken.None);
                Assert.IsTrue(removed.Success);
                var refundedState = await SpaceElevatorServerTestState.ReadAsync(decoy, decoy);
                Assert.AreEqual(supplied, refundedState.PoolAmount);
                Assert.IsFalse(refundedState.DecoyConnected);
                Directory.Delete(directory, true);
            }
            #endregion
        }

        [TearDown]
        public void ResetSceneIsolation() => BlockActionSceneIsolation.Reset();
    }
}
