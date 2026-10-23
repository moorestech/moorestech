using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
using Tests.Util.EnergySystem;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadLaunchFlowTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 初打ち上げ前は建造と発射の合図を待つ()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(3).Count);

            // 建造で資材を取り込み、発射の合図では到着させない
            // Building takes the cargo; a launch command does not cause arrival immediately
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestBuild());
            Assert.IsTrue(inventory.InventoryItems.All(item => item.Count == 0));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            Assert.AreEqual(0f, processor.RequestPower);
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestLaunch());
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, processor.Phase);
            Assert.IsFalse(ServerContext.GetService<IRocketLaunchHistory>().HasLaunchedOnce);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 2));
            Assert.IsTrue(ServerContext.GetService<IRocketLaunchHistory>().HasLaunchedOnce);
            CollectionAssert.AreEqual(new[] { 100, 50 }, inventory.InventoryItems.Skip(5).Select(item => item.Count));
        }

        [Test]
        public void 合図待ちでない台への操作は拒否する()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            ExpectRejection();
            Assert.AreEqual(RocketLaunchPadCommandResult.NotAwaitingBuildCommand, processor.RequestBuild());
            ExpectRejection();
            Assert.AreEqual(RocketLaunchPadCommandResult.NotAwaitingLaunchCommand, processor.RequestLaunch());
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));

            // 合図待ちに入った後の搬出や出力満杯も再検査する
            // Recheck removal of input or output fullness after entering the command wait
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            ExpectRejection();
            Assert.AreEqual(RocketLaunchPadCommandResult.CargoNotReady, processor.RequestBuild());
            inventory.InsertItem(ForUnitTestItemId.RocketCaseId, 2);
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            ExpectRejection();
            Assert.AreEqual(RocketLaunchPadCommandResult.OutputFull, processor.RequestBuild());
        }

        [Test]
        public void 初打ち上げ後は自動で建造し打ち上げる()
        {
            ServerContext.GetService<IRocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var phases = new List<RocketLaunchPadPhase>();
            for (var i = 0; i < 40 && processor.Phase != RocketLaunchPadPhase.Launching; i++)
            {
                GameUpdater.RunFrames(1);
                phases.Add(processor.Phase);
            }
            Assert.AreEqual(RocketLaunchPadPhase.Launching, processor.Phase);
            CollectionAssert.DoesNotContain(phases, RocketLaunchPadPhase.AwaitingBuildCommand);
            CollectionAssert.DoesNotContain(phases, RocketLaunchPadPhase.AwaitingLaunchCommand);
        }

        [Test]
        public void 別の台の初打ち上げで合図待ちは各台の次のtickまでに解ける()
        {
            var a = CreateWaitingPad(Vector3Int.zero);
            var b = CreateWaitingPad(new Vector3Int(10, 0, 0));
            var c = CreateWaitingPad(new Vector3Int(20, 0, 0));
            // 網の再構築でテスト専用発電機は外れるため、全台設置後に再登録する
            // Topology rebuilds remove injected test generators, so register after every pad is placed
            ElectricWireTestUtil.WirePower(Vector3Int.zero, new Vector3Int(3, 0, 0), 100);
            ElectricWireTestUtil.WirePower(new Vector3Int(10, 0, 0), new Vector3Int(13, 0, 0), 100);
            a.GetComponent<IRocketLaunchPadComponent>().RequestBuild();
            b.GetComponent<IRocketLaunchPadComponent>().RequestBuild();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(a, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(b, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));

            // Aの到着通知の中でBは段階だけ変え、自身のUpdateより先に再入発射しない
            // During A's arrival notification B only changes stage and cannot launch before its own update
            var arrivals = 0;
            var history = ServerContext.GetService<IRocketLaunchHistory>();
            using var subscription = history.OnFirstLaunch.Subscribe(_ =>
            {
                Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, b.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
                arrivals++;
            });
            a.GetComponent<IRocketLaunchPadComponent>().RequestLaunch();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(a, RocketLaunchPadPhase.Launching, 2));
            Assert.AreEqual(1, arrivals);
            Assert.That(b.GetComponent<RocketLaunchPadProcessorComponent>().Phase, Is.EqualTo(RocketLaunchPadPhase.ReadyToLaunch).Or.EqualTo(RocketLaunchPadPhase.Launching));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(b, RocketLaunchPadPhase.Launching, 1));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(c, RocketLaunchPadPhase.Building, 1));
        }

        [Test]
        public void 資材が無いときの最小構成()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, processor.Phase);
            Assert.AreEqual(RocketLaunchPadWaitReason.MissingMaterials, processor.WaitReason);
            RocketLaunchPadTestUtil.FillMaterials(pad);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadWaitReason.MissingPayload, processor.WaitReason);
        }

        private static IBlock CreateWaitingPad(Vector3Int position)
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(position, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            return pad;
        }

        private static void ExpectRejection()
        {
            LogAssert.Expect(LogType.Warning, new Regex("ロケット発射台への操作を拒否しました"));
        }

    }
}
