using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
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
            ServerContext.GetService<IRocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 2);
            RocketLaunchPadTestUtil.FillMaterials(pad);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, 40));
            GameUpdater.RunFrames(1);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, processor.WaitReason);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(3).Count);
            CollectionAssert.AreEqual(new[] { 100, 50 }, inventory.InventoryItems.Skip(5).Select(item => item.Count));

            // 150個を置ける空きに戻すと、次のtickで未消費の2便目を取り込む
            // Once 150 outputs fit again, the next tick takes the unconsumed second launch
            inventory.SetItem(5, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Building, processor.Phase);
            Assert.AreEqual(0, inventory.GetItem(3).Count);
        }

        [Test]
        public void 初打ち上げ前でも出力口が満杯なら建造の合図を待たない()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            GameUpdater.RunFrames(5);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(RocketLaunchPadPhase.Idle, processor.Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, processor.WaitReason);
        }

        [Test]
        public void 出力口が満杯でも成果の無い積荷は打ち上げる()
        {
            ServerContext.GetService<IRocketLaunchHistory>().RecordLaunch();
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
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, pad.GetComponent<RocketLaunchPadProcessorComponent>().WaitReason);
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
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, processor.Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.OutputFull, processor.WaitReason);
            Assert.IsFalse(ServerContext.GetService<IRocketLaunchHistory>().HasLaunchedOnce);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(5, ServerContext.ItemStackFactory.CreatEmpty());
            inventory.SetItem(6, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Launching, processor.Phase);
        }
    }
}
