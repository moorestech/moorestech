using System.Collections;
using System.IO;
using System.Linq;
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
                var subInventory = await BlockSubInventoryOpener.Open(view);

                // 実Binderの登録から実API・サーバー応答まで通す
                // Exercise the real binder registration, API and server response
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
