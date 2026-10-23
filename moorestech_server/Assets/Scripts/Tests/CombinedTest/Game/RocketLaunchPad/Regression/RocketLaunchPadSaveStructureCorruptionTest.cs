using System;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchPadSaveStructureCorruptionTest
    {
        // 欠落・null・null要素を積載中と非積載中の両方で検査する
        // Check missing values, nulls and null elements in loaded and unloaded phases
        [Test]
        public void 積荷の構造欠損を警告して待機へ戻す(
            [Values("cargoPayload", "cargoMaterials")] string field,
            [Values("missing", "null", "element")] string corruption,
            [Values(RocketLaunchPadPhase.Building, RocketLaunchPadPhase.AwaitingBuildCommand)] RocketLaunchPadPhase phase)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(phase);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            Corrupt(state, field, corruption);
            LogAssert.Expect(LogType.Warning, new Regex(field + ".*欠損"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            var processor = loaded.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(RocketLaunchPadPhase.Idle, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.IsEmpty(processor.GetRefundItems());
        }

        [Test]
        public void スロットの構造欠損を明示拒否する(
            [Values("inputItems", "outputItems")] string field,
            [Values("missing", "null", "element")] string corruption)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            Corrupt(RocketLaunchPadSaveTestUtil.State(save), field, corruption);
            LogAssert.Expect(LogType.Error, new Regex(field + ".*ロードを拒否"));
            var error = Assert.Throws<Exception>(() => RocketLaunchPadSaveTestUtil.Load(save));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains(field, error.InnerException.Message);
        }

        [Test]
        public void 範囲外の空出力枠は損失として数えない()
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var outputs = (JArray)RocketLaunchPadSaveTestUtil.State(save)["outputItems"];
            var empty = JObject.FromObject(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.CreatEmpty()));
            outputs.Add(empty.DeepClone());
            outputs.Add(empty.DeepClone());
            LogAssert.Expect(LogType.Warning, "セーブに世界作成日時が無いため既定値のままロードします");
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(2, RocketLaunchPadSaveTestUtil.Capture(loaded).OutputItems.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("inputItems", -3)]
        [TestCase("inputItems", 0)]
        [TestCase("outputItems", -3)]
        public void 不正個数を実物損失と区別する(string field, int count)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            var item = state["cargoPayload"].DeepClone();
            item["count"] = count;
            ((JArray)state[field]).Add(item);
            LogAssert.Expect(LogType.Warning, new Regex("復元できない品を 0 個.*不正レコード:1 件"));
            RocketLaunchPadSaveTestUtil.Load(save);
        }

        // 明示ゼロと欠落を区別し、正値はそのまま復元する
        // Distinguish explicit zero from absence and preserve positive remaining time
        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("zero")]
        [TestCase("positive")]
        public void 建造残り時間の欠落を区別する(string value)
        {
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            var state = RocketLaunchPadSaveTestUtil.State(save);
            if (value == "missing") state.Property("remainingSeconds").Remove();
            else state["remainingSeconds"] = value == "null" ? JValue.CreateNull() : new JValue(value == "zero" ? 0d : 2d);
            if (value != "positive")
                LogAssert.Expect(LogType.Warning, new Regex(value == "zero" ? "残り時間が0以下" : "残り時間が欠落"));
            var loaded = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.AreEqual(value == "positive" ? 2d : GameUpdater.SecondsPerTick, RocketLaunchPadSaveTestUtil.Capture(loaded).RemainingSeconds);
        }

        private static void Corrupt(JObject state, string field, string corruption)
        {
            if (corruption == "missing") state.Property(field).Remove();
            else if (corruption == "element" && field != "cargoPayload") state[field] = new JArray(JValue.CreateNull());
            else state[field] = JValue.CreateNull();
        }
    }
}
