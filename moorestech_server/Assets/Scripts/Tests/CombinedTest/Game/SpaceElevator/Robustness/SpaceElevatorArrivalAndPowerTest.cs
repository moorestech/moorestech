using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using Game.Orbit;
using Game.SpaceElevator;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorArrivalAndPowerTest
    {
        [Test]
        public void 到着通知の失敗では衛星とセーブを保持し再試行で一度だけ成果を出す()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = new SpaceElevatorClimberSaveJsonObject
            {
                IsConnected = true,
                ClimbingSatellite = new ItemStackSaveJsonObject(satellite),
                RemainingClimbSeconds = 0,
            };
            var climber = new SpaceElevatorClimberComponent(new Dictionary<string, object> { { "spaceElevatorClimber", save } },
                (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam, inventory);

            // Arriveが復帰しない故障を購読先から注入する
            // Inject an arrival failure through a subscriber so Arrive never returns normally
            var failing = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival
                .Subscribe(_ => throw new InvalidOperationException("injected arrival failure"));
            Assert.Throws<InvalidOperationException>(() => climber.Update());
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);
            Assert.AreEqual(1, ((SpaceElevatorClimberSaveJsonObject)climber.GetSaveState()).ClimbingSatellite.Count);
            Assert.AreEqual(0, inventory.GetItem(1).Count);
            failing.Dispose();

            // 失敗を除くと次tickで再試行し、その後は重複到着しない
            // Retry on the next tick after removing the failure and never arrive twice afterwards
            var arrivals = 0;
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            climber.Update();
            climber.Update();
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(5, inventory.GetItem(1).Count);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
            climber.Destroy();
        }

        [Test]
        public void 出口待ちと成果の事前確認は到着を通知しない()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var arrivals = 0;
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            Assert.AreEqual(0, arrivals);

            // 昇降中に出口を塞ぎ、到着なしで待たせる
            // Block the outlet during ascent and wait without publishing arrival
            inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            RunSeconds(3);
            Assert.AreEqual(0, arrivals);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(block).Status);
            inventory.SetItem(1, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(5, inventory.GetItem(1).Count);
        }

        [TestCase(false, false, false, SpaceElevatorBaseStatus.InsufficientCable)]
        [TestCase(true, false, false, SpaceElevatorBaseStatus.WaitingSatellite)]
        [TestCase(true, true, false, SpaceElevatorBaseStatus.OutputBlocked)]
        [TestCase(true, true, true, SpaceElevatorBaseStatus.OutputBlocked)]
        public void UnchangedIdleTicksDoNotPublishDirectState(bool connected, bool satellite, bool atTop, SpaceElevatorBaseStatus status)
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            if (connected)
            {
                LaunchCables(100);
                ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            }
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var climber = block.GetComponent<SpaceElevatorClimberComponent>();
            if (satellite)
            {
                inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
                if (atTop) RunSeconds(0.5);
                inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            }
            RunSeconds(3);
            Assert.AreEqual(status, ReadDetail(block).Status);
            Assert.AreEqual(atTop ? 1f : 0f, ReadDetail(block).ClimbProgress);

            // パケットの重複除去より上流で、実在庫イベントと直接通知を別々に測る
            // Measure real inventory events and direct state notifications before packet deduplication
            var notifications = 0;
            var inventoryEvents = 0;
            var arrivals = 0;
            using var state = ((IBlockStateObservable)climber).OnChangeBlockState.Subscribe(_ => notifications++);
            using var items = ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated
                .Where(change => change.BlockInstanceId == block.BlockInstanceId).Subscribe(_ => inventoryEvents++);
            using var orbital = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            for (var tick = 0; tick < 8; tick++)
            {
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(status, ReadDetail(block).Status);
                Assert.AreEqual(atTop ? 1f : 0f, ReadDetail(block).ClimbProgress);
                Assert.AreEqual(0, climber.RequestPower);
            }
            TestContext.WriteLine($"idle atTop={atTop} status={status} ticks=8 OnNext={notifications} inventory={inventoryEvents} arrivals={arrivals}");
            Assert.AreEqual(0, inventoryEvents);
            Assert.AreEqual(0, arrivals);
            Assert.AreEqual(0, notifications, $"unchanged {status} atTop={atTop}: direct OnNext={notifications} over 8 ticks");
        }

        [Test]
        public void SuccessfulArrivalAutomaticallyStartsTheNextSatellite()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var arrivals = 0;
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);

            // 二機を先に投入し、一機目の到着後は外部操作なしで次便を完了させる
            // Queue two satellites up front and complete the next trip without further external actions
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 2);
            RunSeconds(2.2);
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, ReadDetail(block).Status);
            RunSeconds(2.2);
            Assert.AreEqual(2, arrivals);
            Assert.AreEqual(10, inventory.GetItem(1).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(block).Status);
        }

        [Test]
        public void 無給電では進まず接続待ちと衛星待ちは電力を要求しない()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var climber = block.GetComponent<SpaceElevatorClimberComponent>();
            var electric = block.GetComponent<SpaceElevatorBaseElectricComponent>();
            Assert.AreEqual(0, electric.RequestEnergy.AsPrimitive());
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            RunSeconds(0.1);
            Assert.AreEqual(0, electric.RequestEnergy.AsPrimitive());
            block.GetComponent<SpaceElevatorBaseInventoryComponent>().SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(3);
            Assert.AreEqual(0f, ReadDetail(block).ClimbProgress);
            Assert.AreEqual(SpaceElevatorBaseStatus.LowPower, ReadDetail(block).Status);
            Assert.AreEqual(100, electric.RequestEnergy.AsPrimitive());
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);
        }
    }
}
