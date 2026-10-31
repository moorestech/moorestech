using System.Linq;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorInletEventRecoveryTest
    {
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        public void LoadedUnsupportedInletRemovalPublishesWithoutAnotherPull(bool returnedCargo, int ticksBeforeRemoval)
        {
            var (_, provider) = CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            if (!returnedCargo) inventory.SetItem(0, ForUnitTestItemId.ItemId1, 1);
            var save = JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            if (returnedCargo)
            {
                // 旧携行品が現マスタで運べず、復元時に入口へ戻る経路
                // Restore cargo no longer carried by the current master back into the inlet
                save["world"][0]["state"]["spaceElevatorClimber"] = JObject.FromObject(new SpaceElevatorClimberSaveJsonObject
                {
                    IsConnected = true,
                    ClimbingSatellite = new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1)),
                    RemainingClimbSeconds = 1,
                });
                LogAssert.Expect(LogType.Error, new Regex("運ぶ積荷ではない"));
            }
            var (_, loaded) = CreateServer();
            ((WorldLoaderFromJson)loaded.GetRequiredService<IWorldSaveDataLoader>()).Load(save.ToString());
            inventory = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SpaceElevatorBaseInventoryComponent>();
            var sink = EventTestUtil.RegisterCaptureSink(loaded, 1);
            new RequestBlockStateProtocol(loaded).GetResponse(MessagePackSerializer.Serialize(
                new RequestBlockStateProtocol.RequestBlockStateProtocolMessagePack(Vector3Int.zero)), 1);
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, Last().Status);
            for (var i = 0; i < ticksBeforeRemoval; i++) GameUpdater.UpdateOneTick();
            sink.TakeAll();

            // 初期pull後は在庫変更とtickだけで実パケットを受信する
            // After the initial pull, receive real packets using only inventory changes and ticks
            inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, Last().Status);
            sink.TakeAll();
            GameUpdater.RunFrames(3);
            Assert.IsFalse(EventTestUtil.ExtractBlockStates(sink.Events).Any(state => (Vector3Int)state.Position == Vector3Int.zero), "idle ticks must not resend the same state");

            #region Internal
            SpaceElevatorBaseStateDetail Last()
            {
                EventTestUtil.CommitBlockStates();
                var states = EventTestUtil.ExtractBlockStates(sink.Events).Where(state => (Vector3Int)state.Position == Vector3Int.zero).ToList();
                Assert.IsNotEmpty(states, "inventory transition emitted no block state in the tick bundle");
                return states.Last()
                    .GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            }
            #endregion
        }

        [Test]
        public void UnsupportedInletAppearanceAndRemovalBothPublish()
        {
            var (_, provider) = CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            GameUpdater.UpdateOneTick();
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.ItemId1, 1);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, LastStatus());
            sink.TakeAll();
            inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, LastStatus());

            #region Internal
            SpaceElevatorBaseStatus LastStatus()
            {
                EventTestUtil.CommitBlockStates();
                var states = EventTestUtil.ExtractBlockStates(sink.Events).Where(state => (Vector3Int)state.Position == Vector3Int.zero).ToList();
                Assert.IsNotEmpty(states, "inventory transition emitted no block state in the tick bundle");
                return states.Last()
                    .GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey).Status;
            }
            #endregion
        }

    }
}
