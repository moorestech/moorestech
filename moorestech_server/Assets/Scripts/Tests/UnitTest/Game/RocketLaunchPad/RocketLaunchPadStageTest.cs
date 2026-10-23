using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Block.Interface.Extension;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;
using Core.Item.Interface;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadStageTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 空の積荷では積載段階を作れない()
        {
            var empty = new RocketCargo(ServerContext.ItemStackFactory.CreatEmpty(), Array.Empty<IItemStack>());

            // 積荷を持つ全段階で同じ不変条件を保証する
            // Every loaded stage enforces the same invariant
            Assert.Throws<ArgumentException>(() => new BuildingStage(empty, 10));
            Assert.Throws<ArgumentException>(() => new AwaitingLaunchCommandStage(empty));
            Assert.Throws<ArgumentException>(() => new ReadyToLaunchStage(empty));
        }

        [Test]
        public void 残りゼロの進行段階は作れない()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingStage(CreateCargo(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LaunchingStage(0));
        }

        [Test]
        public void 積荷と進行時間は該当する段階だけが持つ()
        {
            var cargo = CreateCargo();
            RocketLaunchPadStage building = new BuildingStage(cargo, 10);
            RocketLaunchPadStage awaitingLaunch = new AwaitingLaunchCommandStage(cargo);
            RocketLaunchPadStage ready = new ReadyToLaunchStage(cargo);
            RocketLaunchPadStage launching = new LaunchingStage(20);

            // 撤去返却と時間進行は段階の型から判別する
            // Removal refunds and time progression are determined from stage types
            Assert.IsInstanceOf<LoadedRocketStage>(building);
            Assert.IsInstanceOf<LoadedRocketStage>(awaitingLaunch);
            Assert.IsInstanceOf<LoadedRocketStage>(ready);
            Assert.IsNotInstanceOf<LoadedRocketStage>(IdleStage.Instance);
            Assert.IsNotInstanceOf<LoadedRocketStage>(AwaitingBuildCommandStage.Instance);
            Assert.IsNotInstanceOf<LoadedRocketStage>(launching);
            Assert.AreSame(cargo, ((LoadedRocketStage)building).Cargo);
            Assert.AreSame(cargo, ((LoadedRocketStage)awaitingLaunch).Cargo);
            Assert.AreSame(cargo, ((LoadedRocketStage)ready).Cargo);

            Assert.IsInstanceOf<IProgressingStage>(building);
            Assert.IsInstanceOf<IProgressingStage>(launching);
            Assert.IsNotInstanceOf<IProgressingStage>(ready);
            Assert.IsNotInstanceOf<IProgressingStage>(awaitingLaunch);
            Assert.IsNotInstanceOf<IProgressingStage>(IdleStage.Instance);
            Assert.IsNotInstanceOf<IProgressingStage>(AwaitingBuildCommandStage.Instance);
            Assert.AreEqual(10u, ((IProgressingStage)building).RemainingTicks);
            Assert.AreEqual(20u, ((IProgressingStage)launching).RemainingTicks);
        }

        [Test]
        public void 各段階は固定した通信とセーブの段階値を返す()
        {
            var cargo = CreateCargo();
            RocketLaunchPadStage[] stages =
            {
                IdleStage.Instance, AwaitingBuildCommandStage.Instance, new BuildingStage(cargo, 1),
                new AwaitingLaunchCommandStage(cargo), new ReadyToLaunchStage(cargo), new LaunchingStage(1),
            };

            // セーブ済みの整数値と型の対応を固定する
            // Pin the mapping between stage types and persisted integer values
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, stages.Select(stage => (int)stage.Phase));
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, stages[4].Phase);
        }

        [Test]
        public void 撤去返却は積荷と空でない資材を含む()
        {
            var payload = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.SatellitePayloadId, 1);
            var material = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketCaseId, 2);
            var cargo = new RocketCargo(payload, new[] { material, ServerContext.ItemStackFactory.CreatEmpty() });

            // 欠損マスタで空になった資材は返却品から除外する
            // Exclude materials emptied by missing-master pruning from the refund
            Assert.IsTrue(cargo.IsLoaded);
            CollectionAssert.AreEqual(new[] { payload, material }, cargo.AllItems());
        }

        [Test]
        public void 未対応の段階はログ付きで拒否し元の段階を保つ()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var sequence = typeof(RocketLaunchPadProcessorComponent).GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            var setStage = sequence.GetType().GetMethod("SetStage");

            // Phaseだけ既知値を返す未知型も、通常待機として扱わない
            // Reject an unknown type even if it reports a known phase
            LogAssert.Expect(LogType.Error, new Regex("発射台の未対応の段階を拒否しました"));
            var exception = Assert.Throws<TargetInvocationException>(() => setStage.Invoke(sequence, new object[] { new UnknownStage() }));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, processor.Phase);
        }

        private sealed class UnknownStage : RocketLaunchPadStage
        {
            public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Idle;
        }

        private static RocketCargo CreateCargo()
        {
            return new RocketCargo(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.SatellitePayloadId, 1), Array.Empty<IItemStack>());
        }
    }
}
