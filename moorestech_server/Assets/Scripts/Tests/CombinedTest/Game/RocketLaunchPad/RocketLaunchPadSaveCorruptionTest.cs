using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Update;
using Core.Master;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadSaveCorruptionTest
    {
        [Test]
        public void 入れ口の無い品は警告して捨てる()
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            var item = new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ComputeDataId, 1));
            ((JArray)state["inputItems"]).Add(JObject.FromObject(item));
            LogAssert.Expect(LogType.Warning, new Regex("復元できない品"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(RocketLaunchPadPhase.Building, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
            Assert.IsFalse(JObject.FromObject(RocketLaunchPadSaveTestUtil.Capture(loaded))["inputItems"].ToString().Contains(item.ItemGuidStr));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(10)]
        public void 空か未登録か一便と異なる個数の積荷は警告して待機に戻す(int count)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            var itemId = count == 0 ? ItemMaster.EmptyItemId : count == 1 ? ForUnitTestItemId.ComputeDataId : ForUnitTestItemId.CablePayloadId;
            state["cargoPayload"] = JObject.FromObject(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(itemId, count)));
            LogAssert.Expect(LogType.Warning, new Regex("打ち上げられないため積荷を捨てます"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
            Assert.IsEmpty(loaded.GetComponent<RocketLaunchPadProcessorComponent>().GetRefundItems());
        }

        [Test]
        public void 負の巡回位置は積荷表の範囲へ正規化する()
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            RocketLaunchPadSaveTestUtil.State(save)["nextPayloadIndex"] = -1;
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(1, RocketLaunchPadSaveTestUtil.Capture(loaded).NextPayloadIndex);
        }

        [Test]
        public void 不明な段階は警告して待機へ戻る()
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            RocketLaunchPadSaveTestUtil.State(save)["phase"] = 99;
            LogAssert.Expect(LogType.Warning, new Regex("段階の値が不明"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
        }

        [TestCase("cargoPayload")]
        [TestCase("cargoMaterials")]
        [TestCase("inputItems")]
        [TestCase("outputItems")]
        public void 上限超過の生DTOを生成前に捨てる(string field)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            if (field == "cargoPayload") state[field]["count"] = 101;
            else if (field == "cargoMaterials") state[field][0]["count"] = 101;
            else
            {
                var item = JObject.FromObject(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ComputeDataId, 1)));
                item["count"] = 101;
                if (field == "outputItems") { state[field][0] = item.DeepClone(); state[field][1] = item.DeepClone(); }
                ((JArray)state[field]).Add(item);
            }
            LogAssert.Expect(LogType.Warning, new Regex(field.StartsWith("cargo") ? "打ち上げられない" : "復元できない品"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(field.StartsWith("cargo") ? RocketLaunchPadPhase.Idle : RocketLaunchPadPhase.Building, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
        }

        // 個数ゼロでも実体化前にGUID構文を検証する
        // Validate GUID syntax before materialization even for zero counts
        [TestCase("cargoMaterials", "broken-guid")]
        [TestCase("cargoMaterials", null)]
        [TestCase("outputItems", "broken-guid")]
        [TestCase("outputItems", null)]
        [TestCase("cargoMaterials", "00000000-0000-0000-0000-000000000000")]
        [TestCase("outputItems", "00000000-0000-0000-0000-000000000000")]
        public void 個数ゼロのGUIDを生成前に検証する(string field, string guid)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var item = new JObject { ["count"] = 0 };
            if (guid != null) item["itemGuid"] = guid;
            RocketLaunchPadSaveTestUtil.State(save)[field][0] = item;
            var valid = guid == "00000000-0000-0000-0000-000000000000";
            if (!valid) LogAssert.Expect(LogType.Warning, new Regex(field == "cargoMaterials" ? "打ち上げられない" : "復元できない品"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(!valid && field == "cargoMaterials" ? RocketLaunchPadPhase.Idle : RocketLaunchPadPhase.Building,
                RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
            var restored = JObject.FromObject(RocketLaunchPadSaveTestUtil.Capture(loaded));
            if (field == "outputItems") Assert.AreEqual(0, (int)restored[field][0]["count"]);
            if (!valid && field == "cargoMaterials") Assert.IsEmpty(loaded.GetComponent<RocketLaunchPadProcessorComponent>().GetRefundItems());
        }

        [TestCase("phase")]
        [TestCase("nextPayloadIndex")]
        public void 必須ゼロ値の欠落を警告する(string key)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            state["phase"] = (int)RocketLaunchPadPhase.Launching;
            state["cargoPayload"] = JObject.FromObject(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.CreatEmpty()));
            state["cargoMaterials"] = new JArray();
            RocketLaunchPadSaveTestUtil.State(save).Property(key).Remove();
            LogAssert.Expect(LogType.Warning, new Regex(key == "phase" ? "段階の値が不明" : "巡回位置が無い"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(key == "phase" ? RocketLaunchPadPhase.Idle : RocketLaunchPadPhase.Launching, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
            if (key == "nextPayloadIndex") Assert.AreEqual(0, RocketLaunchPadSaveTestUtil.Capture(loaded).NextPayloadIndex);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        public void 打ち上げ中の残り時間が0以下なら警告して一tickへ直す(double seconds)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            state["phase"] = (int)RocketLaunchPadPhase.Launching;
            state["remainingSeconds"] = seconds;
            state["cargoPayload"] = JObject.FromObject(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.CreatEmpty()));
            state["cargoMaterials"] = new JArray();
            LogAssert.Expect(LogType.Warning, new Regex("残り時間が0以下のため1tick"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(GameUpdater.SecondsPerTick, RocketLaunchPadSaveTestUtil.Capture(loaded).RemainingSeconds);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(loaded.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
        }
    }
}
