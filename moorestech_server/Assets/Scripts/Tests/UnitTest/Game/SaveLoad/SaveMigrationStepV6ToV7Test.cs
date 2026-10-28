using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    // 版6のパイプ(double の amount / velocity)が版7(10 億分の 1 単位の整数 amountNano / velocityNano)へ変換されるか
    // Whether version-6 pipes (double amount / velocity) convert to version 7 (10^-9-unit integer amountNano / velocityNano)
    public class SaveMigrationStepV6ToV7Test
    {
        private const string PipeKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";
        private const string TankKey = "Game.Fluid.FluidContainerSaveJsonObject";
        private const string FailureLog = "版6から版7へ変換できません";

        [Test]
        public void パイプの量と面速度が10億倍の最近傍整数になり機械のタンクは変わらないTest()
        {
            var pipe = JObject.Parse("{\"fluidGuid\":\"e2a5d2e5-3fa5-47e2-9fc0-68c8552523f8\",\"amount\":49.9374561815308,\"faceVelocities\":[{\"x\":1,\"y\":0,\"z\":0,\"velocity\":0.0103214}]}");
            var tank = JObject.Parse("{\"amount\":12.3456}");
            var save = Save(new JObject { ["blockGuid"] = "pipe", ["state"] = new JObject { [PipeKey] = pipe, [TankKey] = tank } });

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var converted = (JObject)result.Save["world"][0]["state"][PipeKey];
            Assert.IsNull(converted["amount"]);
            Assert.AreEqual(49_937_456_182L, converted["amountNano"].Value<long>());
            Assert.AreEqual("e2a5d2e5-3fa5-47e2-9fc0-68c8552523f8", converted["fluidGuid"].Value<string>());
            var face = (JObject)converted["faceVelocities"][0];
            Assert.IsNull(face["velocity"]);
            Assert.AreEqual(10_321_400L, face["velocityNano"].Value<long>());
            Assert.AreEqual(1, face["x"].Value<int>());
            // 機械のタンクは double のまま
            // The machine tank stays a double
            Assert.AreEqual(12.3456, result.Save["world"][0]["state"][TankKey]["amount"].Value<double>(), 1e-12);
        }

        [Test]
        public void 変換済みや空の面速度やstateの無いブロックはそのまま通るTest()
        {
            var save = Save(
                new JObject { ["state"] = new JObject { [PipeKey] = JObject.Parse("{\"fluidGuid\":\"\",\"amountNano\":5,\"faceVelocities\":[]}") } },
                new JObject { ["state"] = new JObject { [PipeKey] = JObject.Parse("{\"fluidGuid\":\"\",\"amount\":0,\"faceVelocities\":[]}") } },
                new JObject { ["state"] = new JObject { [PipeKey] = JObject.Parse("{\"fluidGuid\":\"\",\"amount\":2.5}") } },
                new JObject { ["blockGuid"] = "chest" });

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(5L, result.Save["world"][0]["state"][PipeKey]["amountNano"].Value<long>());
            Assert.AreEqual(0L, result.Save["world"][1]["state"][PipeKey]["amountNano"].Value<long>());
            Assert.AreEqual(2_500_000_000L, result.Save["world"][2]["state"][PipeKey]["amountNano"].Value<long>());
            Assert.IsNull(result.Save["world"][3]["state"]);
        }

        [Test]
        public void amountが数値でない形は拒否されるTest()
        {
            var save = Save(new JObject { ["state"] = new JObject { [PipeKey] = JObject.Parse("{\"fluidGuid\":\"\",\"amount\":\"many\",\"faceVelocities\":[]}") } });
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("amount", result.FailureReason);
        }

        [Test]
        public void 面速度にvelocityが無い形は拒否されるTest()
        {
            var save = Save(new JObject { ["state"] = new JObject { [PipeKey] = JObject.Parse("{\"fluidGuid\":\"\",\"amount\":1,\"faceVelocities\":[{\"x\":1,\"y\":0,\"z\":0}]}") } });
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("velocity", result.FailureReason);
        }

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 6, ["world"] = new JArray(blocks) };
        }
    }
}
