using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV5ToV6Test
    {
        private const string OldKey = SaveMigrationStepV5ToV6.OldBeltSaveKey;
        private const string SiblingKey = "GearEnergyTransformer";
        private const string FailureLog = "版5から版6へ変換できません";

        [Test]
        public void 旧ベルコンのstateだけが除去され同じブロックの他キーは残るTest()
        {
            var oldItems = JArray.Parse("[\"{\\\"id\\\":1,\\\"count\\\":1}\",null,\"{\\\"id\\\":2,\\\"count\\\":1}\",null]");
            var sibling = JObject.Parse("{\"rpm\":10,\"torque\":2}");
            var save = Save(new JObject { ["blockGuid"] = "belt", ["state"] = new JObject { [OldKey] = oldItems, [SiblingKey] = sibling } });

            // 旧キーを落とし、隣のキーは値ごと保つ
            // Drop the old key and keep the sibling key with its value
            var result = new SaveMigrationStepV5ToV6().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var state = (JObject)result.Save["world"][0]["state"];
            Assert.IsNull(state[OldKey]);
            Assert.AreEqual(1, state.Count);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse("{\"rpm\":10,\"torque\":2}"), state[SiblingKey]));
            Assert.AreEqual("belt", result.Save["world"][0]["blockGuid"].Value<string>());
        }

        [Test]
        public void 旧キーの値がnullや空配列や全nullでも除去されるTest()
        {
            var save = Save(
                new JObject { ["state"] = new JObject { [OldKey] = JValue.CreateNull() } },
                new JObject { ["state"] = new JObject { [OldKey] = new JArray() } },
                new JObject { ["state"] = new JObject { [OldKey] = JArray.Parse("[null,null,null,null]") } });

            var result = new SaveMigrationStepV5ToV6().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            for (var i = 0; i < 3; i++)
            {
                Assert.IsFalse(((JObject)result.Save["world"][i]["state"]).ContainsKey(OldKey), $"block {i}");
                Assert.AreEqual(0, ((JObject)result.Save["world"][i]["state"]).Count, $"block {i}");
            }
        }

        [Test]
        public void 旧キーを持たないブロックは素通しされるTest()
        {
            var save = Save(
                JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                JObject.Parse("{\"blockGuid\":\"y\",\"state\":{}}"),
                JObject.Parse("{\"blockGuid\":\"z\"}"),
                JObject.Parse("{\"blockGuid\":\"w\",\"state\":null}"),
                JObject.Parse("{\"state\":{\"Game.Block.Blocks.BeltConveyor.Save.BeltConveyorSaveStateComponent\":{\"items\":[]}}}"));
            var original = save.DeepClone();

            var result = new SaveMigrationStepV5ToV6().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, result.Save));
        }

        [Test]
        public void 再実行しても変化せず版はステップが書き換えないTest()
        {
            var save = Save(
                new JObject { ["state"] = new JObject { [OldKey] = JArray.Parse("[\"{}\",null]"), [SiblingKey] = new JObject() } },
                JObject.Parse("{\"state\":{\"ChestComponent\":{}}}"));

            var first = new SaveMigrationStepV5ToV6().Migrate(save);
            Assert.IsTrue(first.IsConverted, first.FailureReason);

            // 2回目は旧キーが無いので何も変えない
            // The second run finds no old key and changes nothing
            var once = first.Save.DeepClone();
            var second = new SaveMigrationStepV5ToV6().Migrate(first.Save);
            Assert.IsTrue(second.IsConverted, second.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(once, second.Save));
            Assert.AreEqual(5, second.Save["worldVersion"].Value<int>());
        }

        [Test]
        public void FromVersionは4であるTest()
        {
            Assert.AreEqual(5, new SaveMigrationStepV5ToV6().FromVersion);
        }

        // 辿れない外部データは原本を変えず拒否する
        // Refuse unwalkable external data without touching the original
        [TestCase("{\"world\":{}}")]
        [TestCase("{\"world\":\"broken\"}")]
        [TestCase("{}")]
        [TestCase("{\"world\":[null]}")]
        [TestCase("{\"world\":[1]}")]
        [TestCase("{\"world\":[[]]}")]
        [TestCase("{\"world\":[{\"state\":[]}]}")]
        [TestCase("{\"world\":[{\"state\":\"broken\"}]}")]
        [TestCase("{\"world\":[{\"state\":1}]}")]
        public void 辿れない構造は理由つきで拒否するTest(string json)
        {
            var save = JObject.Parse(json);
            save["worldVersion"] = 5;
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV5ToV6().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        // 旧ベルコンとして読めない値は消さず拒否する
        // Refuse a value unreadable as an old belt instead of erasing it
        [TestCase("{}")]
        [TestCase("{\"items\":[]}")]
        [TestCase("\"broken\"")]
        [TestCase("\"[]\"")]
        [TestCase("123")]
        [TestCase("true")]
        [TestCase("[{}]")]
        [TestCase("[123]")]
        [TestCase("[null,\"{}\",[]]")]
        [TestCase("[true]")]
        public void 旧ベルコンとして読めない値はキー名つきで拒否するTest(string oldValueJson)
        {
            var save = Save(new JObject { ["state"] = new JObject { [OldKey] = JToken.Parse(oldValueJson), [SiblingKey] = new JObject() } });
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV5ToV6().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains(OldKey, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 5, ["world"] = new JArray(blocks) };
        }
    }
}
