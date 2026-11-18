using System.Linq;
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
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 1便3個の積荷を運ぶ昇降中の状態が、セーブと撤去で個数ごと保たれることを確かめる
    // Verify a climbing three-per-launch payload keeps its count through save/load and removal
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

            // 読み直すと昇降中のまま、入口は空で、昇っている3個は保たれる
            // After reload it is still climbing, the inlet stays empty, and the three climbing items are kept
            var (_, loadedProvider) = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.Greater(ReadDetail(loaded).ClimbProgress, 0f);
            Assert.AreEqual(3, loaded.GetComponent<SpaceElevatorClimberComponent>().GetRefundItems()[0].Count);

            // ロード後の電力網へ発電機を戻し、残りだけで一便ぶんの成果が出る
            // Restore generation to the loaded network; one launch worth of results appears within the remainder
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
    }
}
