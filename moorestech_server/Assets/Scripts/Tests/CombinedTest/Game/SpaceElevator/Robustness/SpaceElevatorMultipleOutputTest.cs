using System.Linq;
using Core.Inventory;
using Core.Item;
using Game.Block.Blocks.Service;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Core.Item.Interface;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.State;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Core.Other;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorMultipleOutputTest : SpaceElevatorMultipleOutputFixture
    {
        [Test]
        public void 空きの後ろに同種があれば確認で原本を変えず全成果を格納する()
        {
            var block = PlaceBase(Vector3Int.zero);
            var outputs = ServerContext.GetService<IOrbitalArrivalService>().PreviewArrivalOutputs(ForUnitTestItemId.TestElevatorSatellite, 1);
            var arrival = new FixedArrival(outputs);
            var inventory = CreateInventory(block, arrival);
            inventory.SetItem(2, ForUnitTestItemId.TestElevatorReward, 5);
            var before = inventory.InventoryItems.ToArray();
            var changes = 0;
            using var subscription = ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated
                .Where(change => change.BlockInstanceId == block.BlockInstanceId).Subscribe(_ => changes++);

            // I1の反例をそのまま使い、確認の副作用も検査する
            // Use the exact I1 counterexample and check the preview has no side effects
            Assert.AreEqual(10, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestElevatorReward));
            Assert.AreEqual(10, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.RocketComputerId));
            var fits = InventoryInsertItem.TryFillAllPreferringExisting(outputs, inventory.InventoryItems.Skip(1).ToList());
            for (var i = 0; i < before.Length; i++) Assert.AreSame(before[i], inventory.GetItem(i));
            Assert.AreEqual(0, changes);
            Assert.AreEqual(5, outputs[0].Count);
            Assert.AreEqual(5, outputs[1].Count);
            Assert.IsTrue(fits, "[空,A5]へ[A5,B5]は[B5,A10]として入る");

            // 実挿入も同じ配置となり、両出口だけを通知する
            // Real insertion uses the same placement and notifies both outlets only
            Assert.IsTrue(inventory.TryCompleteArrival(ForUnitTestItemId.TestElevatorSatellite));
            Assert.AreEqual(1, arrival.Previews);
            Assert.AreEqual(1, arrival.Arrivals);
            AssertOutputs(inventory);
            Assert.AreEqual(2, changes);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 同属性だけを既存スタックに加算する(bool matchingMetadata)
        {
            var block = PlaceBase(Vector3Int.zero);
            var existing = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 5).SetMeta("quality", new TestMeta1());
            var incoming = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 5)
                .SetMeta("quality", matchingMetadata ? (ItemStackMetaData)new TestMeta1() : new TestMeta2());
            var outputs = new[] { incoming, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketComputerId, 5) };
            var inventory = CreateInventory(block, new FixedArrival(outputs));
            inventory.SetItem(2, existing);

            // 同IDでも属性が違えば空き2枠が必要で、1枠では拒否する
            // Different metadata needs two free slots even for the same ID and cannot fit one
            Assert.AreEqual(matchingMetadata, InventoryInsertItem.TryFillAllPreferringExisting(outputs, inventory.InventoryItems.Skip(1).ToList()));
            Assert.AreSame(existing, inventory.GetItem(2));
            Assert.AreEqual(0, inventory.GetItem(1).Count);
            if (matchingMetadata)
            {
                Assert.IsTrue(inventory.TryCompleteArrival(ForUnitTestItemId.TestElevatorSatellite));
                AssertOutputs(inventory);
                Assert.IsInstanceOf<TestMeta1>(inventory.GetItem(2).GetMeta("quality"));
            }
        }

        [Test]
        public void 同種の残容量を使い切ってから余りを空きへ置く()
        {
            var outputs = new[] { ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 5) };
            var inventory = CreateInventory(PlaceBase(Vector3Int.zero), new FixedArrival(outputs));
            inventory.SetItem(2, ForUnitTestItemId.TestElevatorReward, 8);

            // 既存8へ2を加え、残り3だけが空きへ入る
            // Add two to the existing eight and put only the remaining three into the empty slot
            Assert.IsTrue(InventoryInsertItem.TryFillAllPreferringExisting(outputs, inventory.InventoryItems.Skip(1).ToList()));
            Assert.AreEqual(8, inventory.GetItem(2).Count);
            Assert.IsTrue(inventory.TryCompleteArrival(ForUnitTestItemId.TestElevatorSatellite));
            Assert.AreEqual(10, inventory.GetItem(2).Count);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, inventory.GetItem(1).Id);
            Assert.AreEqual(3, inventory.GetItem(1).Count);
        }

        [Test]
        public void 複数成果が入る配置なら開始前に誤って出口待ちに入らない()
        {
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(2, ForUnitTestItemId.TestElevatorReward, 5);
            var arrivals = 0;
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);

            // 開始前に反例を作り、プレビューだけでは到着させない
            // Set up the counterexample before starting and never arrive from a preview alone
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.2);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, ReadDetail(block).Status);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(0, arrivals);
            Assert.AreEqual(5, inventory.GetItem(2).Count);

            RunSeconds(2.2);
            AssertOutputs(inventory);
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(block).Status);
        }

        [Test]
        public void 昇降中に複数成果が入る配置になれば頂上で誤って出口待ちに入らない()
        {
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var arrivals = 0;
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, ReadDetail(block).Status);

            // 昇降中に反例を作り、頂上の再検査と実到着を通す
            // Set up the counterexample during ascent and exercise the top check and real arrival
            inventory.SetItem(2, ForUnitTestItemId.TestElevatorReward, 5);
            Assert.AreEqual(0, arrivals);
            RunSeconds(2.2);
            AssertOutputs(inventory);
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(block).Status);
            Assert.AreEqual(0f, block.GetComponent<SpaceElevatorClimberComponent>().RequestPower);
        }

        private static SpaceElevatorBaseInventoryComponent CreateInventory(IBlock block, IOrbitalArrivalService arrival)
        {
            var param = (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, block.BlockPositionInfo);
            return new SpaceElevatorBaseInventoryComponent(block.BlockInstanceId, param,
                new ConnectingInventoryListPriorityInsertItemService(block.BlockInstanceId, connector), arrival);
        }

        private sealed class FixedArrival : IOrbitalArrivalService
        {
            internal int Previews;
            internal int Arrivals;
            private readonly System.Collections.Generic.IReadOnlyList<IItemStack> _outputs;
            internal FixedArrival(System.Collections.Generic.IReadOnlyList<IItemStack> outputs) { _outputs = outputs; }
            public System.IObservable<OrbitalArrival> OnOrbitalArrival => Observable.Empty<OrbitalArrival>();
            public bool IsOrbitalPayload(global::Core.Master.ItemId id) => true;
            public System.Collections.Generic.IReadOnlyList<IItemStack> PreviewArrivalOutputs(global::Core.Master.ItemId id, int count) { Previews++; return _outputs; }
            public System.Collections.Generic.IReadOnlyList<IItemStack> Arrive(global::Core.Master.ItemId id, int count) { Arrivals++; return _outputs; }
        }

        private static void AssertOutputs(SpaceElevatorBaseInventoryComponent inventory)
        {
            Assert.AreEqual(ForUnitTestItemId.RocketComputerId, inventory.GetItem(1).Id);
            Assert.AreEqual(5, inventory.GetItem(1).Count);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, inventory.GetItem(2).Id);
            Assert.AreEqual(10, inventory.GetItem(2).Count);
        }
    }
}
