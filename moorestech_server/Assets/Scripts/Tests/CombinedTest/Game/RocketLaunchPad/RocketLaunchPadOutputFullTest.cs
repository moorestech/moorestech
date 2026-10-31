using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.World.Interface.DataStore;
using Game.Orbit;
using Game.SpaceElevator;
using Game.SpaceElevator.Save;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadOutputFullTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 成果が出力口に入りきらないときは建造を始めない()
        {
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 2);
            RocketLaunchPadTestUtil.FillMaterials(pad);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, 40));
            GameUpdater.RunFrames(1);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, RocketLaunchPadTestUtil.ReadState(processor).WaitReason);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(3).Count);
            CollectionAssert.AreEqual(new[] { 100, 50 }, inventory.InventoryItems.Skip(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlotStart).Select(item => item.Count));

            // 150個を置ける空きに戻すと、次のtickで未消費の2便目を取り込む
            // Once 150 outputs fit again, the next tick takes the unconsumed second launch
            inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0], ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Building, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.AreEqual(0, inventory.GetItem(3).Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ケーブルプール満杯なら資材を消費せず建造前に待つ(bool launchedOnce)
        {
            if (launchedOnce) ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.TestElevatorCable, 50);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var before = inventory.InventoryItems.ToArray();
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();

            // 建造前も到着時と同じプール容量を検査し、入力を保持する
            // Check the same pool capacity before building as at arrival, preserving inputs
            GameUpdater.RunFrames(5);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, RocketLaunchPadTestUtil.ReadState(processor).WaitReason);
            CollectionAssert.AreEqual(before, inventory.InventoryItems);
            Assert.IsEmpty(processor.GetRefundItems());
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue - 50 });
            GameUpdater.RunFrames(1);
            Assert.AreEqual(launchedOnce ? RocketLaunchPadPhase.Building : RocketLaunchPadPhase.AwaitingBuildCommand,
                RocketLaunchPadTestUtil.ReadState(processor).Phase);
        }

        [Test]
        public void 初打ち上げ前でも出力口が満杯なら建造の合図を待たない()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            GameUpdater.RunFrames(5);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, RocketLaunchPadTestUtil.ReadState(processor).WaitReason);
        }

        [Test]
        public void 出力口が満杯でも成果の無い積荷は打ち上げる()
        {
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            pad.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(ForUnitTestItemId.CablePayloadId, 5);
            var arrivals = new List<OrbitalArrival>();
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(arrivals.Add);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, arrivals.Single().PayloadItemId);

            // 成果なしの便だけを飛ばし、残った衛星は満杯の出力口を待つ
            // Fly only the output-free cargo and leave the satellite waiting for output space
            RocketLaunchPadTestUtil.FillMaterials(pad);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, 40));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, RocketLaunchPadTestUtil.ReadState(pad.GetComponent<RocketLaunchPadProcessorComponent>()).WaitReason);
        }

        [Test]
        public void 出力口の成果を接続先へ流し入れ口は搬出しない()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.right, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var chest);
            RocketLaunchPadTestUtil.FillMaterials(pad);
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 75);
            GameUpdater.RunFrames(1);
            var output = chest.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(150, output.InventoryItems.Where(item => item.Id == ForUnitTestItemId.ComputeDataId).Sum(item => item.Count));
            var input = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(2, input.GetItem(0).Count);
            Assert.IsTrue(input.InventoryItems.Skip(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlotStart).All(item => item.Count == 0));
        }

        [Test]
        public void 打ち上げ直前に出力口が埋まったら発射を待たせる()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            processor.RequestBuild();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            processor.RequestLaunch();
            GameUpdater.RunFrames(5);
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, RocketLaunchPadTestUtil.ReadState(processor).WaitReason);
            Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0], ServerContext.ItemStackFactory.CreatEmpty());
            inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1], ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Launching, RocketLaunchPadTestUtil.ReadState(processor).Phase);
        }
    }
}
