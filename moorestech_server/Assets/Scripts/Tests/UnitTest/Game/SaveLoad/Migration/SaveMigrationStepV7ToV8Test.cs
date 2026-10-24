using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV7ToV8Test
    {
        private const string ProcessorKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string WireKey = "Game.Block.Blocks.ElectricWire.ElectricWireConnectorComponent";
        private const string FailureLog = "版7から版8へ変換できません";

        [Test]
        public void 液体キーの無い清浄室機械へ空配列が足され他のキーは残るTest()
        {
            // 版7の清浄室機械の実際の形（継承しないレシピの継承レベルは null）
            // The actual version-7 clean-room machine shape (a non-inheritance recipe stores a null inherited level)
            var processor = JObject.Parse("{\"cycleCount\":3,\"state\":0,\"inputSlot\":[],\"inheritedLevel\":null}");
            var save = Save(new JObject { ["blockGuid"] = "m", ["state"] = new JObject { [ProcessorKey] = processor, [WireKey] = new JObject { ["wires"] = new JArray() } } });

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migrated = (JObject)result.Save["world"][0]["state"][ProcessorKey];
            Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["inputFluidSlot"]));
            Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["outputFluidSlot"]));
            Assert.AreEqual(3, migrated["cycleCount"].Value<int>());
            Assert.AreEqual(JTokenType.Null, migrated["inheritedLevel"].Type);
            Assert.IsNotNull(result.Save["world"][0]["state"][WireKey]);
        }

        [Test]
        public void 清浄室機械でないブロックと既に液体キーを持つブロックは素通しされるTest()
        {
            var save = Save(
                JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                JObject.Parse("{\"blockGuid\":\"y\"}"),
                JObject.Parse("{\"blockGuid\":\"z\",\"state\":null}"),
                new JObject { ["state"] = new JObject { [ProcessorKey] = JObject.Parse("{\"inputFluidSlot\":[{\"fluidGuid\":\"g\",\"amount\":5}],\"outputFluidSlot\":[]}") } });
            var original = save.DeepClone();

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, result.Save));
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

        [Test]
        public void FromVersionは7であるTest()
        {
            Assert.AreEqual(7, new SaveMigrationStepV7ToV8().FromVersion);
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
