using System.Linq;
using UniRx;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Util;
using Tests.Util.Rocket;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadSaveLoadTest
    {
        [TestCase(RocketLaunchPadPhase.Building)]
        [TestCase(RocketLaunchPadPhase.AwaitingLaunchCommand)]
        [TestCase(RocketLaunchPadPhase.ReadyToLaunch)]
        public void 積載状態は別DIへの保存往復で一致し進行を再開する(RocketLaunchPadPhase phase)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(phase);
            var before = RocketLaunchPadSaveTestUtil.State(save);
            Assert.AreEqual(JTokenType.Integer, before["phase"].Type);
            Assert.AreEqual((int)phase, before["phase"].Value<int>());
            if (phase == RocketLaunchPadPhase.Building) Assert.AreEqual(0.65d, before["remainingSeconds"].Value<double>(), 0.00001d);
            Assert.AreEqual(1, before["nextPayloadIndex"].Value<int>());

            // 実スロット・積荷・残秒・巡回位置が完全一致する
            // Real slots, cargo, remaining seconds and the rotation index must match exactly
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            var processor = loaded.GetComponent<RocketLaunchPadProcessorComponent>();
            var after = JObject.FromObject(RocketLaunchPadSaveTestUtil.Capture(loaded));
            Assert.IsTrue(JToken.DeepEquals(before, after), $"before={before}\nafter={after}");
            Assert.AreEqual(phase, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            TestContext.WriteLine($"保存復元 phase={phase} remainingSeconds={after["remainingSeconds"]} nextPayloadIndex={after["nextPayloadIndex"]} JSON同一=true");
            RocketLaunchPadSaveTestUtil.ClearOutputs(loaded);
            for (var i = 0; i < 40 && RocketLaunchPadTestUtil.ReadState(processor).Phase == RocketLaunchPadPhase.Building; i++)
            {
                processor.SupplyPower(100);
                GameUpdater.RunFrames(1);
            }
            if (RocketLaunchPadTestUtil.ReadState(processor).Phase == RocketLaunchPadPhase.AwaitingLaunchCommand) processor.RequestLaunch();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(loaded, RocketLaunchPadPhase.Launching, 2));
            Assert.IsTrue(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
        }

        [TestCase(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadPhase.AwaitingBuildCommand)]
        [TestCase(RocketLaunchPadPhase.AwaitingLaunchCommand, RocketLaunchPadPhase.ReadyToLaunch)]
        public void 初打ち上げ済みなら発射待ちは読込時に建造待ちはtickで解ける(RocketLaunchPadPhase saved, RocketLaunchPadPhase immediately)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(saved);
            save["rocketHasLaunchedOnce"] = true;
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            var processor = loaded.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(immediately, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(saved == RocketLaunchPadPhase.AwaitingBuildCommand ? RocketLaunchPadPhase.Building : RocketLaunchPadPhase.Launching, RocketLaunchPadTestUtil.ReadState(processor).Phase);
        }

        [Test]
        public void 正常な打ち上げ中は残期間と成果を保存し到着を再通知しない()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            processor.RequestBuild();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            processor.RequestLaunch();
            GameUpdater.RunFrames(3);

            // 正常な発射の途中から実ワールド全体を別DIへ保存往復する
            // Round-trip the whole real world into a separate DI container during a normal launch
            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(services));
            var before = RocketLaunchPadSaveTestUtil.State(save);
            var remainingTicks = GameUpdater.SecondsToTicks(before["remainingSeconds"].Value<double>());
            Assert.Greater(remainingTicks, 2u);
            var (_, restoredServices) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var arrivals = 0;
            using var subscription = restoredServices.GetRequiredService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            SaveLoadJsonTestHelper.LoadFromJson(restoredServices, save.ToString());
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            var restored = loaded.GetComponent<RocketLaunchPadProcessorComponent>();
            var after = JObject.FromObject(RocketLaunchPadSaveTestUtil.Capture(loaded));
            Assert.IsTrue(JToken.DeepEquals(before, after));
            Assert.AreEqual(0, after["cargoPayload"]["count"].Value<int>());
            Assert.IsEmpty((JArray)after["cargoMaterials"]);
            Assert.AreEqual(150, after["outputItems"].Sum(item => item["count"].Value<int>()));
            Assert.AreEqual(0, arrivals);

            // 残り期間を1tickずつ観測し、最後のtickでだけIdleへ戻る
            // Observe each remaining tick and return to Idle only on the final tick
            for (var i = 1u; i < remainingTicks; i++)
            {
                GameUpdater.RunFrames(1);
                Assert.AreEqual(RocketLaunchPadPhase.Launching, RocketLaunchPadTestUtil.ReadState(restored).Phase);
                var current = RocketLaunchPadSaveTestUtil.Capture(loaded);
                Assert.AreEqual(remainingTicks - i, GameUpdater.SecondsToTicks(current.RemainingSeconds.Value));
            }
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(restored).Phase);
            Assert.AreEqual(0, arrivals);
            Assert.AreEqual(150, RocketLaunchPadSaveTestUtil.Capture(loaded).OutputItems.Sum(item => item.Count));
            TestContext.WriteLine($"Launching保存復元 remainingTicks={remainingTicks}, outputs=150, replayedArrivals={arrivals}");
        }

        [Test]
        public void 長過ぎる残り時間でも進捗は負にならない()
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            RocketLaunchPadSaveTestUtil.State(save)["remainingSeconds"] = 100;
            var pad = RocketLaunchPadSaveTestUtil.Load(save);
            var detail = pad.GetComponent<RocketLaunchPadProcessorComponent>().GetBlockStateDetails()[0];
            var state = MessagePackSerializer.Deserialize<RocketLaunchPadStateDetail>(detail.Value);
            Assert.AreEqual(0f, state.Progress);
        }
    }
}
