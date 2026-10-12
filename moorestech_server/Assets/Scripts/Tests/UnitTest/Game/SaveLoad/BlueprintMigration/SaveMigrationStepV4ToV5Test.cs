using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.BlueprintMigration
{
    public class SaveMigrationStepV4ToV5Test
    {
        [Test]
        public void BPのオフセットが最小角基準へ移り配線リストが空で付くTest()
        {
            var save = new JObject
            {
                ["worldVersion"] = 4,
                ["blueprints"] = new JArray(new JObject
                {
                    ["name"] = "a", ["guid"] = Guid.NewGuid().ToString(),
                    ["blocks"] = new JArray(Block(-2, 0, -1), Block(1, 2, 3)),
                }),
            };

            var result = new SaveMigrationStepV4ToV5().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var blocks = result.Save["blueprints"][0]["blocks"];
            Assert.AreEqual(0, blocks[0]["offsetX"].Value<int>());
            Assert.AreEqual(0, blocks[0]["offsetZ"].Value<int>());
            Assert.AreEqual(3, blocks[1]["offsetX"].Value<int>());
            Assert.AreEqual(2, blocks[1]["offsetY"].Value<int>());
            Assert.AreEqual(4, blocks[1]["offsetZ"].Value<int>());
            Assert.AreEqual(0, result.Save["blueprints"][0]["wires"].Count());
            Assert.AreEqual(0, result.Save["blueprints"][0]["chains"].Count());
        }

        [Test]
        public void 現在カルチャの負号が異なっても負のオフセットを移行するTest()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var alternateCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            alternateCulture.NumberFormat.NegativeSign = "−";
            try
            {
                CultureInfo.CurrentCulture = alternateCulture;
                var save = new JObject
                {
                    ["blueprints"] = new JArray(new JObject
                    {
                        ["blocks"] = new JArray(Block(-2, 0, 0), Block(1, 0, 0))
                    })
                };

                var result = new SaveMigrationStepV4ToV5().Migrate(save);

                Assert.IsTrue(result.IsConverted, result.FailureReason);
                Assert.AreEqual(3, save["blueprints"][0]["blocks"][1]["offsetX"].Value<int>());
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void BP節が無いセーブもそのまま変換されるTest()
        {
            var result = new SaveMigrationStepV4ToV5().Migrate(new JObject { ["worldVersion"] = 4 });
            Assert.IsTrue(result.IsConverted, result.FailureReason);
        }

        [Test]
        public void blocksが配列でなければ失敗理由を返すTest()
        {
            LogAssert.Expect(LogType.Warning, new Regex("版4から版5へ変換できません"));
            var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = 1 }) };
            Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
        }

        [Test]
        public void blocks要素がオブジェクトでなければ失敗理由を返すTest()
        {
            LogAssert.Expect(LogType.Warning, new Regex("blocks要素がオブジェクトではありません"));
            var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = new JArray(1) }) };
            Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
        }

        [Test]
        public void offsetが欠けていれば失敗理由を返し書き換えないTest()
        {
            LogAssert.Expect(LogType.Warning, new Regex("整数のoffsetYがありません"));
            var broken = Block(1, 0, 1);
            broken.Remove("offsetY");
            var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = new JArray(Block(5, 5, 5), broken) }) };
            var result = new SaveMigrationStepV4ToV5().Migrate(save);
            Assert.IsFalse(result.IsConverted);
            Assert.AreEqual(5, save["blueprints"][0]["blocks"][0]["offsetX"].Value<int>());
        }

        [Test]
        public void 全BPを検証してから書き換えるTest()
        {
            var save = new JObject
            {
                ["blueprints"] = new JArray(
                    new JObject { ["blocks"] = new JArray(Block(5, 5, 5)) },
                    new JObject { ["blocks"] = new JArray(false) })
            };
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("blocks要素がオブジェクトではありません"));

            Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [TestCase("{\"blueprints\":1}")]
        [TestCase("{\"blueprints\":[null]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":2147483648,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":0.5,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":-2147483649,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":9223372036854775807,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":9223372036854775808,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        [TestCase("{\"blueprints\":[{\"blocks\":[{\"offsetX\":-9223372036854775809,\"offsetY\":0,\"offsetZ\":0}]}]}")]
        public void 不正な外部JSONは例外でなく失敗を返すTest(string json)
        {
            var save = JObject.Parse(json);
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版4から版5へ変換できません"));

            var result = new SaveMigrationStepV4ToV5().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 平行移動で整数が桁あふれするBPを拒否するTest()
        {
            var save = new JObject
            {
                ["blueprints"] = new JArray(new JObject
                {
                    ["blocks"] = new JArray(Block(int.MinValue, 0, 0), Block(int.MaxValue, 0, 0))
                })
            };
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("幅が整数範囲外"));

            Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 空BPと再実行を扱い版番号は連鎖に任せるTest()
        {
            var save = new JObject
            {
                ["worldVersion"] = 4,
                ["blueprints"] = new JArray(
                    new JObject { ["blocks"] = new JArray() },
                    new JObject { ["blocks"] = new JArray(Block(-5, -3, -2), Block(2, 4, 5)) })
            };
            var step = new SaveMigrationStepV4ToV5();
            Assert.IsTrue(step.Migrate(save).IsConverted);
            var migrated = save.DeepClone();
            Assert.IsTrue(step.Migrate(save).IsConverted);

            Assert.IsTrue(JToken.DeepEquals(migrated, save));
            Assert.AreEqual(4, save["worldVersion"].Value<int>());
            Assert.AreEqual(0, save["blueprints"][0]["wires"].Count());
            Assert.AreEqual(0, save["blueprints"][0]["chains"].Count());
            Assert.AreEqual(7, save["blueprints"][1]["blocks"][1]["offsetY"].Value<int>());
        }

        private static JObject Block(int x, int y, int z)
        {
            return new JObject
            {
                ["offsetX"] = x,
                ["offsetY"] = y,
                ["offsetZ"] = z,
                ["blockGuid"] = Guid.Empty.ToString(),
                ["direction"] = 0,
                ["settings"] = new JObject()
            };
        }
    }
}
