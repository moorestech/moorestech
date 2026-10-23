using System.Collections;
using System.IO;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
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
                var master = MasterHolder.BlockMaster.GetBlockAllIds().Select(id => MasterHolder.BlockMaster.GetBlockMaster(id))
                    .Single(b => b.BlockParam is SpaceElevatorBaseBlockParam);
                var position = new Vector3Int(20, 0, 4);
                var target = PlaceBlock(master.Name, position, BlockDirection.North);
                var decoy = PlaceBlock(master.Name, Vector3Int.zero, BlockDirection.North);
                var view = await WaitBlockGameObjectSpawn(position);
                // 実配置でクリック・所有者・操作を検証
                // Verify clicking, ownership and interaction on the placed block
                var collider = view.GetComponentsInChildren<Collider>(true).Single();
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
                Debug.Log($"[SpaceElevatorClick] count=1 layer={collider.gameObject.layer} clickable=true owner={child.BlockGameObject.name} actions={view.Interactable.Actions.Count}");
                var subInventory = await BlockSubInventoryOpener.Open(view);

                // 実Binder・API・応答を通す
                // Exercise the real binder, API and server response
                var insufficient = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsFalse(insufficient.Ok);
                Assert.AreEqual("insufficient_cable", insufficient.Error);
                var param = (SpaceElevatorBaseBlockParam)master.BlockParam;
                var cable = MasterHolder.ItemMaster.GetItemId(param.CableItemGuid);
                var perLaunch = MasterHolder.ItemMaster.Items.OrbitalPayloads.Single(p => p.PayloadItemGuid == param.CableItemGuid).CountPerLaunch;
                var pool = ServerContext.GetService<SpaceElevatorCablePool>();
                while (pool.Amount < param.CablesPerElevator) ServerContext.GetService<IOrbitalArrivalService>().Arrive(cable, perLaunch);
                var connected = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsTrue(connected.Ok, connected.Error);
                Assert.IsTrue(target.GetComponent<SpaceElevatorClimberComponent>().IsConnected);
                Assert.IsFalse(decoy.GetComponent<SpaceElevatorClimberComponent>().IsConnected, "request used the decoy coordinates");
                var already = await WebUiActionInvoker.ExecuteAsync("space_elevator.connect", new JObject());
                Assert.IsFalse(already.Ok);
                Assert.AreEqual("already_connected", already.Error);
                BlockSubInventoryOpener.Close(subInventory);
                Directory.Delete(directory, true);
            }
            #endregion
        }
    }
}
