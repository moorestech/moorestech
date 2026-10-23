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
            Assert.AreEqual(phase, processor.Phase);
            TestContext.WriteLine($"保存復元 phase={phase} remainingSeconds={after["remainingSeconds"]} nextPayloadIndex={after["nextPayloadIndex"]} JSON同一=true");
            RocketLaunchPadSaveTestUtil.ClearOutputs(loaded);
            for (var i = 0; i < 40 && processor.Phase == RocketLaunchPadPhase.Building; i++)
            {
                processor.SupplyPower(100);
                GameUpdater.RunFrames(1);
            }
            if (processor.Phase == RocketLaunchPadPhase.AwaitingLaunchCommand) processor.RequestLaunch();
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
            Assert.AreEqual(immediately, processor.Phase);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(saved == RocketLaunchPadPhase.AwaitingBuildCommand ? RocketLaunchPadPhase.Building : RocketLaunchPadPhase.Launching, processor.Phase);
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
