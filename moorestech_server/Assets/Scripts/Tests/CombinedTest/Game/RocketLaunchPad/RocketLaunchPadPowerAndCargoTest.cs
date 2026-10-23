using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.Save;
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
    public class RocketLaunchPadPowerAndCargoTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 電力が半分なら建造が倍かかり電力無しは止まる()
        {
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            GameRandom.Reseed(12345);
            var full = MeasureBuildTicks(Vector3Int.zero, 100);
            var half = MeasureBuildTicks(new Vector3Int(10, 0, 0), 50);
            TestContext.WriteLine($"建造tick full={full} half={half} ratio={half / (float)full}");
            Assert.That(half / (float)full, Is.InRange(1.6f, 2.4f));

            // 実電力網で供給0にし、消費要求を残したまま進行が止まることを確かめる
            // Use a real zero-supply network and verify progress stops while demand remains
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(new Vector3Int(20, 0, 0), 0);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Building, 5));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var before = ((RocketLaunchPadSaveJsonObject)processor.GetSaveState()).RemainingSeconds;
            GameUpdater.RunFrames(40);
            Assert.AreEqual(RocketLaunchPadPhase.Building, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.NoPower, RocketLaunchPadTestUtil.ReadState(processor).WaitReason);
            Assert.AreEqual(100, processor.RequestPower);
            Assert.AreEqual(before, ((RocketLaunchPadSaveJsonObject)processor.GetSaveState()).RemainingSeconds);

            #region Internal

            int MeasureBuildTicks(Vector3Int position, float power)
            {
                var pad = RocketLaunchPadTestUtil.PlacePoweredPad(position, power);
                RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
                Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Building, 5));
                var ticks = 0;
                while (RocketLaunchPadTestUtil.ReadState(pad.GetComponent<RocketLaunchPadProcessorComponent>()).Phase == RocketLaunchPadPhase.Building && ticks < 100)
                {
                    GameUpdater.RunFrames(1);
                    ticks++;
                }
                Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(pad.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
                return ticks;
            }

            #endregion
        }

        [Test]
        public void 揃った積荷は交互に選ぶ()
        {
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 4);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.CablePayloadId, 20);
            RocketLaunchPadTestUtil.FillMaterials(pad);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var arrivals = new List<OrbitalArrival>();
            using var subscription = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(arrivals.Add);
            // 両候補を残し続け、固定先頭探索への退行を検出する
            // Keep both candidates available to detect regression to a fixed first choice
            for (var tick = 0; tick < 200 && arrivals.Count < 3; tick++)
            {
                inventory.SetItem(5, ServerContext.ItemStackFactory.CreatEmpty());
                inventory.SetItem(6, ServerContext.ItemStackFactory.CreatEmpty());
                GameUpdater.RunFrames(1);
            }
            CollectionAssert.AreEqual(new[] { ForUnitTestItemId.SatellitePayloadId, ForUnitTestItemId.CablePayloadId, ForUnitTestItemId.SatellitePayloadId }, arrivals.Select(arrival => arrival.PayloadItemId));
            Assert.AreEqual(5, arrivals[1].PayloadCount);
        }

        [Test]
        public void 撤去で積んだ資材を返し発射後は返さない()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            processor.RequestBuild();
            AssertCargoRefund();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            AssertCargoRefund();
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            processor.RequestLaunch();
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            AssertCargoRefund();

            // 発射前の積載は返却し、軌道到着後は消費済みとして返さない
            // Refund loaded contents before launch and none after orbital arrival
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(5, ServerContext.ItemStackFactory.CreatEmpty());
            inventory.SetItem(6, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 2));
            Assert.IsEmpty(processor.GetRefundItems());

            #region Internal
            void AssertCargoRefund()
            {
                var items = processor.GetRefundItems();
                CollectionAssert.AreEquivalent(new[] { ForUnitTestItemId.RocketCaseId, ForUnitTestItemId.RocketFuelId, ForUnitTestItemId.RocketComputerId, ForUnitTestItemId.SatellitePayloadId }, items.Select(item => item.Id));
                Assert.AreEqual(7, items.Sum(item => item.Count));
            }
            #endregion
        }

    }
}
