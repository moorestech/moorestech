using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8CleanRoomFluidTest
    {
        private const string ProcessorKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string WireKey = "Game.Block.Blocks.ElectricWire.ElectricWireConnectorComponent";
        private const string FailureLog = "版7から版8へ変換できません";

        [Test]
        public void 液体キーの無い清浄室機械へ空配列が足され他のキーは残るTest()
        {
            // 版7の清浄室機械の実際の形（液体キーも継承レベルも無い）
            // The actual version-7 clean-room machine shape (neither fluid keys nor an inherited level)
            var processor = JObject.Parse("{\"cycleCount\":3,\"state\":0,\"inputSlot\":[]}");
            var save = Save(new JObject { ["blockGuid"] = "m", ["state"] = new JObject { [ProcessorKey] = processor, [WireKey] = new JObject { ["wires"] = new JArray() } } });

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migrated = (JObject)result.Save["world"][0]["state"][ProcessorKey];
            Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["inputFluidSlot"]));
            Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["outputFluidSlot"]));
            Assert.AreEqual(3, migrated["cycleCount"].Value<int>());
            Assert.IsNotNull(result.Save["world"][0]["state"][WireKey]);
        }

        [Test]
        public void 清浄室機械でないブロックと既に液体キーを持つブロックは素通しされるTest()
        {
            var save = Save(
                JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                JObject.Parse("{\"blockGuid\":\"y\"}"),
                JObject.Parse("{\"blockGuid\":\"z\",\"state\":null}"),
                new JObject { ["state"] = new JObject { [ProcessorKey] = JObject.Parse("{\"inheritedLevel\":null,\"inputFluidSlot\":[{\"fluidGuid\":\"g\",\"amount\":5}],\"outputFluidSlot\":[]}") } });
            var originalWorld = save["world"].DeepClone();

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(originalWorld, result.Save["world"]));
        }

        [Test]
        public void processorが明示nullの清浄室機械は飛ばして台数をログに残すTest()
        {
            // ロード側もnullを復元見送りとして扱うので、変換を止めず台数だけ残す
            // The loader also treats null as skip-restore, so keep converting and only record the count
            var save = Save(new JObject { ["state"] = new JObject { [ProcessorKey] = JValue.CreateNull() } });
            LogAssert.Expect(LogType.Log, new Regex("processorが null で飛ばした清浄室機械=1台"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(JTokenType.Null, result.Save["world"][0]["state"][ProcessorKey].Type);
        }

        [Test]
        public void 清浄室機械の液体キーが明示nullなら空配列で補うTest()
        {
            // 明示nullは欠損と同じ扱い（判断記録 A18）
            // An explicit null is treated like a missing key (decision A18)
            var save = Save(JObject.Parse($"{{\"state\":{{\"{ProcessorKey}\":{{\"inputFluidSlot\":null,\"outputFluidSlot\":null}}}}}}"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, ((JArray)result.Save["world"][0]["state"][ProcessorKey]["inputFluidSlot"]).Count);
            Assert.AreEqual(0, ((JArray)result.Save["world"][0]["state"][ProcessorKey]["outputFluidSlot"]).Count);
        }

        [Test]
        public void 再実行しても変化せず版はステップが書き換えないTest()
        {
            var save = Save(new JObject { ["state"] = new JObject { [ProcessorKey] = new JObject() } });

            var first = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(first.IsConverted, first.FailureReason);
            var once = first.Save.DeepClone();
            var second = new SaveMigrationStepV7ToV8().Migrate(first.Save);

            Assert.IsTrue(second.IsConverted, second.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(once, second.Save));
            Assert.AreEqual(7, second.Save["worldVersion"].Value<int>());
        }

        // 辿れない形は理由つきで拒否し、原本を変えない
        // Refuse unwalkable shapes with a reason and leave the original untouched
        [TestCase("{\"world\":{}}")]
        [TestCase("{}")]
        [TestCase("{\"world\":[1]}")]
        [TestCase("{\"world\":[{\"state\":[]}]}")]
        [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":[]}}]}")]
        [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":{\"inputFluidSlot\":\"broken\"}}}]}")]
        [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":{\"outputFluidSlot\":1}}}]}")]
        public void 辿れない構造は理由つきで拒否するTest(string json)
        {
            var save = JObject.Parse(json);
            save["worldVersion"] = 7;
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 後ろのブロックが壊れていても前のブロックを書き換えないTest()
        {
            var save = Save(
                new JObject { ["state"] = new JObject { [ProcessorKey] = new JObject() } },
                new JObject { ["state"] = new JObject { [ProcessorKey] = new JArray() } });
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        #region TestHelper

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 7, ["world"] = new JArray(blocks) };
        }

        #endregion
    }
}
