using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Block.Interface.Component;
using Core.Item;
using Game.Context;
using Game.SpaceElevator;
using NUnit.Framework;
using Game.Block.Interface.Extension;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorClimbTest
    {
        [Test]
        public void 接続済みで電力が足りれば2秒で昇り成果が出口に出る()
        {
            var elevatorBase = SetupConnected(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 2);

            RunSeconds(2.2);

            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, Inventory(elevatorBase).GetItem(1).Id);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(1).Count);
        }

        [Test]
        public void 電力が半分なら倍かかる()
        {
            var elevatorBase = SetupConnected(50f);
            // 確率丸めの系列を固定し電力比の検証を安定させる
            // Fix probabilistic rounding so the power-ratio assertion is reproducible
            global::Core.Update.GameRandom.Reseed(0);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);

            RunSeconds(2.2);
            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(1).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.LowPower, ReadDetail(elevatorBase).Status);

            RunSeconds(2.4);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(1).Count);
        }

        [Test]
        public void 未接続なら昇らず衛星は入口に残る()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);

            RunSeconds(3);

            Assert.AreEqual(1, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 出口が塞がっていると昇り始めず空くと昇る()
        {
            var elevatorBase = SetupConnected(100f);
            var inventory = Inventory(elevatorBase);

            // 出口を別アイテムで塞ぐと、衛星は入口に残り理由を出す
            // Blocking the outlet with another item keeps the satellite in the inlet and shows why
            inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(elevatorBase).Status);
            Assert.AreEqual(1, inventory.GetItem(0).Count);
            Assert.AreEqual(0f, ReadDetail(elevatorBase).ClimbProgress);

            // 出口を空けると昇り始め、2秒で成果が出る
            // Clearing the outlet starts the climb and the result appears after two seconds
            inventory.SetItem(1, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(2.2);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(5, inventory.GetItem(1).Count);
        }

        [Test]
        public void 昇る間に出口が塞がると頂上で電力を使わず待ち空くと渡す()
        {
            var elevatorBase = SetupConnected(100f);
            var inventory = Inventory(elevatorBase);
            var climber = elevatorBase.GetComponent<SpaceElevatorClimberComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);

            // 昇降中に出口を塞ぐと、昇り切っても渡さず頂上で待つ
            // Blocking the outlet mid-climb makes it wait at the top without handing over
            inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            RunSeconds(2);
            var waiting = ReadDetail(elevatorBase);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, waiting.Status);
            Assert.AreEqual(1f, waiting.ClimbProgress);
            Assert.AreEqual(0f, climber.RequestPower);

            // 出口を空けると次のtickで渡し、成果が出る
            // Clearing the outlet hands it over on the next tick and the result appears
            inventory.SetItem(1, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(0.1);
            Assert.AreEqual(5, inventory.GetItem(1).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 入口は運ぶ積荷だけを受ける()
        {
            CreateServer();
            var inventory = Inventory(PlaceBase(Vector3Int.zero));
            var rejected = inventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            Assert.AreEqual(3, rejected.Count);
            Assert.IsFalse(inventory.IsAllowedToPlace(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 1)));
        }

        [Test]
        public void 成果が一部しか入らないと在庫を変えず待ちちょうど入れば昇る()
        {
            var block = SetupConnected(100f);
            var inventory = Inventory(block);
            var capacity = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestElevatorReward);
            inventory.SetItem(1, ForUnitTestItemId.TestElevatorReward, capacity - 4);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.1);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(block).Status);
            Assert.AreEqual(capacity - 4, inventory.GetItem(1).Count);
            Assert.AreEqual(1, inventory.GetItem(0).Count);

            // 成果5個がちょうど入る境界で再開する
            // Resume at the exact capacity boundary for five reward items
            inventory.SetItem(1, ForUnitTestItemId.TestElevatorReward, capacity - 5);
            RunSeconds(2.2);
            Assert.AreEqual(capacity, inventory.GetItem(1).Count);
        }

        [Test]
        public void 隣のチェストには出口だけを搬出し入口の衛星は残る()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId,
                new Vector3Int(1, 0, 0), BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var chest));
            var inventory = Inventory(block);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 2);
            inventory.SetItem(1, ForUnitTestItemId.TestElevatorReward, 5);
            RunSeconds(0.1);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(0, inventory.GetItem(1).Count);
            Assert.AreEqual(5, chest.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);
        }

        [TestCase("empty", 0)]
        [TestCase("all", 0)]
        [TestCase("partial", 1)]
        [TestCase("rejected", 2)]
        public void 一括挿入は実際の余りだけ返す(string scenario, int expected)
        {
            CreateServer();
            var inventory = Inventory(PlaceBase(Vector3Int.zero));
            var item = scenario == "rejected" ? ForUnitTestItemId.ItemId1 : ForUnitTestItemId.TestElevatorSatellite;
            if (scenario == "partial") inventory.SetItem(0, item, ItemStackLevelDataStore.Instance.GetMaxStack(item) - 1);
            var items = new List<IItemStack>();
            if (scenario != "empty") items.Add(ServerContext.ItemStackFactory.Create(item, 2));
            var remainders = inventory.InsertItem(items);
            Assert.AreEqual(expected == 0 ? 0 : 1, remainders.Count);
            if (0 < expected)
            {
                Assert.AreEqual(expected, remainders[0].Count);
                Assert.AreEqual(item, remainders[0].Id);
            }
            Assert.AreEqual(scenario == "rejected" || scenario == "empty" ? 0 : scenario == "partial" ? ItemStackLevelDataStore.Instance.GetMaxStack(item) : 2,
                inventory.GetItem(0).Count);
        }

        private static IBlock SetupConnected(float power)
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, power);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            return elevatorBase;
        }

        private static SpaceElevatorBaseInventoryComponent Inventory(IBlock elevatorBase)
        {
            return elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
        }
    }
}
