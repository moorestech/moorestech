using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorBaseSaveAndRefundTest
    {
        private const int PlayerId = 3;

        [Test]
        public void 接続と昇降中がセーブを往復する()
        {
            var (_, provider) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(1);
            var json = provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 読み直すと接続済みで昇降中のまま、残り1秒で昇り切る
            // After reload it is still connected and climbing, finishing within the remaining second
            var (_, loadedProvider) = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsTrue(ReadDetail(loaded).IsConnected);
            Assert.Greater(ReadDetail(loaded).ClimbProgress, 0f);
            Assert.AreEqual(0, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);

            // ロードで置かれた拠点も接続サービスが追跡し、撤去で接続分がプールへ戻る
            // The connection service also tracks a base placed by loading, and removing it returns the connection to the pool
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, loadedProvider.GetService<SpaceElevatorConnectionService>().Connect(loaded));
            // ロード後の電力網へ発電機を戻し、残りだけで完了する
            // Restore generation to the loaded network and finish within the saved remainder
            global::Core.Update.GameUpdater.UpdateOneTick();
            Assert.IsTrue(ServerContext.GetService<global::Game.EnergySystem.IElectricWireNetworkLookup>()
                .TryGetEnergySegment(loaded.BlockInstanceId, out var segment));
            Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil.AddGenerator(segment,
                new Tests.Module.TestElectricGenerator(new global::Game.EnergySystem.ElectricPower(100), BlockInstanceId.Create()));
            RunSeconds(1.2);
            Assert.AreEqual(5, loaded.GetComponent<SpaceElevatorBaseInventoryComponent>().GetItem(1).Count);
            Assert.AreEqual(0, loaded.GetComponent<SpaceElevatorClimberComponent>().GetRefundItems().Count);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            Assert.AreEqual(100, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 撤去すると昇降中の衛星とスロットがプレイヤーへ返り接続分がプールへ戻る()
        {
            var (packet, provider) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 3);
            RunSeconds(0.5);

            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(Vector3Int.zero)), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));

            // 入口に2機・昇降中に1機の計3機が戻る
            // Two in the inlet and one climbing come back, three in total
            var playerInventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var satellites = playerInventory.InventoryItems.Where(item => item.Id == ForUnitTestItemId.TestElevatorSatellite).Sum(item => item.Count);
            Assert.AreEqual(3, satellites);
            Assert.AreEqual(100, provider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void ロードした残りが昇降時間を超えていたら総時間に収める()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var param = (SpaceElevatorBaseBlockParam)elevatorBase.BlockMasterElement.BlockParam;
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellite), RemainingClimbSeconds = param.ClimbSeconds * 10 };

            // マスタで昇降時間を縮めた後のロードを模す。進みは負にならず0から始まる
            // Mimic loading after the master shortened the climb; the progress starts at zero instead of going negative
            LogAssert.Expect(LogType.Warning, new Regex("総時間に収めます"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(0f, ReadClimberDetail(climber).ClimbProgress);
        }

        [Test]
        public void 運ぶ積荷でない衛星が昇降中でロードされたら入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var stray = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(stray), RemainingClimbSeconds = 1 };

            // 頂上で軌道到着サービスが例外を出さないよう、昇降を取り消して入口へ戻す
            // Cancel the climb and return it to the inlet so the orbital service never throws at the top
            LogAssert.Expect(LogType.Error, new Regex("運ぶ積荷ではない"));
            var climber = LoadClimber(elevatorBase, save);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            Assert.AreEqual(ForUnitTestItemId.ItemId1, inventory.GetItem(0).Id);
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, ReadClimberDetail(climber).Status);

            // 入口の運べないアイテムは運ばず、入口を塞いだ tick に開発者ログへ出す
            // The non-carried item in the inlet is never carried, and the tick it starts blocking is logged
            LogAssert.Expect(LogType.Warning, new Regex("運ぶ積荷ではないため昇れません"));
            climber.Update();
            climber.Update();
            Assert.AreEqual(ForUnitTestItemId.ItemId1, inventory.GetItem(0).Id);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
        }

        [Test]
        public void 昇降中の衛星が2機以上のセーブは1機だけ昇らせ残りを入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var satellites = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 3);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellites), RemainingClimbSeconds = 1 };

            // 壊れたセーブを模す。1機だけ昇り、余りの2機は入口へ戻る
            // Mimic a broken save; one satellite keeps climbing and the extra two go back to the inlet
            LogAssert.Expect(LogType.Error, new Regex("1機だけ昇らせ"));
            var climber = LoadClimber(elevatorBase, save);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);
        }

        [Test]
        public void 残りが負のセーブは最初から昇り直す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellite), RemainingClimbSeconds = -5 };

            // 負の残りで即座に頂上へ着かせない
            // A negative remaining time must not jump straight to the top
            LogAssert.Expect(LogType.Error, new Regex("最初から昇り直します"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(0f, ReadClimberDetail(climber).ClimbProgress);
            Assert.AreEqual(1, climber.GetRefundItems().Count);
        }

        private static SpaceElevatorClimberComponent LoadClimber(IBlock elevatorBase, SpaceElevatorClimberSaveJsonObject save)
        {
            var param = (SpaceElevatorBaseBlockParam)elevatorBase.BlockMasterElement.BlockParam;
            var states = new Dictionary<string, object> { { "spaceElevatorClimber", save } };
            return new SpaceElevatorClimberComponent(states, param, elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>());
        }

        private static SpaceElevatorBaseStateDetail ReadClimberDetail(SpaceElevatorClimberComponent climber)
        {
            return MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(climber.GetBlockStateDetails()[0].Value);
        }
    }
}
