using Core.Item.Interface;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 1便3個の昇降中状態がセーブ・撤去で保たれる
    // A climbing three-per-launch state survives save/load and removal
    public class SpaceElevatorBulkPayloadSaveTest
    {
        private const int PlayerId = 3;

        [Test]
        public void 一便ぶんの積荷を昇降中にセーブしてロードすると続きから昇り一便ぶんの成果が出る()
        {
            var (_, provider) = CreateServer();
            var elevatorBase = PlaceConnectedBase(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 3);
            RunSeconds(1);
            var json = provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 昇降中のまま入口は空、3個は保持
            // Still climbing, inlet empty, three items kept
            var (_, loadedProvider) = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.Greater(ReadDetail(loaded).ClimbProgress, 0f);
            Assert.AreEqual(3, loaded.GetComponent<SpaceElevatorClimberComponent>().GetRefundItems()[0].Count);

            // 発電機を戻し、残りで一便ぶん成果が出る
            // Restore the generator; one launch of results appears
            global::Core.Update.GameUpdater.UpdateOneTick();
            Assert.IsTrue(ServerContext.GetService<IElectricWireNetworkLookup>().TryGetEnergySegment(loaded.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(100), BlockInstanceId.Create()));
            RunSeconds(1.2);
            Assert.AreEqual(7, Inventory(loaded).GetItem(1).Count);
            Assert.AreEqual(0, Inventory(loaded).GetItem(0).Count);
            Assert.AreEqual(0, loaded.GetComponent<SpaceElevatorClimberComponent>().GetRefundItems().Count);
        }

        [Test]
        public void 撤去すると昇降中の一便ぶんと入口の端数がプレイヤーへ返る()
        {
            var (packet, provider) = CreateServer();
            var elevatorBase = PlaceConnectedBase(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 4);
            RunSeconds(0.5);
            Assert.AreEqual(3, elevatorBase.GetComponent<SpaceElevatorClimberComponent>().GetRefundItems()[0].Count);

            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(Vector3Int.zero)), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));

            // 入口に1個・昇降中に3個の計4個が戻る
            // One in the inlet and three climbing come back, four in total
            var playerInventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var returned = playerInventory.InventoryItems.Where(item => item.Id == ForUnitTestItemId.TestElevatorBulkPayload).Sum(item => item.Count);
            Assert.AreEqual(4, returned);
        }

        [Test]
        public void 一便に足りない個数の積荷が昇降中でロードされたら昇降を取り消して入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var shortPayload = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorBulkPayload, 2);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(shortPayload), RemainingClimbSeconds = 1 };

            // 1便3個に2個では頂上で例外になるため、昇らせず入口へ全量を戻す
            // Two of three per launch would throw at the top, so return everything to the inlet without climbing
            LogAssert.Expect(LogType.Error, new Regex("1便の .* 個に足りない"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorBulkPayload, Inventory(elevatorBase).GetItem(0).Id);
            Assert.AreEqual(2, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
        }

        [Test]
        public void 一便より多い個数の積荷が昇降中でロードされたら一便ぶんだけ昇らせ余りを入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var excess = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorBulkPayload, 5);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(excess), RemainingClimbSeconds = 1 };

            // 1便3個だけ昇り、余り2個が入口に戻る
            // Only the three of one launch climb and the remaining two return to the inlet
            LogAssert.Expect(LogType.Error, new Regex("1便の .* 個だけ昇らせ"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(2, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(3, climber.GetRefundItems()[0].Count);
        }
    }
}
