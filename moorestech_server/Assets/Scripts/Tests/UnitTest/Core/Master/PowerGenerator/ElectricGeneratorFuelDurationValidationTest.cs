using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.PowerGenerator
{
    public class ElectricGeneratorFuelDurationValidationTest
    {
        private MasterJsonFileContainer _original;
        [SetUp] public void Setup()
        {
            _original = new MasterJsonFileContainer(ReadConfigs());
            MasterHolder.Load(_original);
        }
        [TearDown] public void RestoreMaster() => MasterHolder.Load(_original);

        [TestCase("fuelItems", 0d)]
        [TestCase("fuelItems", -1d)]
        [TestCase("fuelItems", double.NaN)]
        [TestCase("fuelItems", double.PositiveInfinity)]
        [TestCase("fuelFluids", 0d)]
        [TestCase("fuelFluids", -1d)]
        [TestCase("fuelFluids", double.NaN)]
        [TestCase("fuelFluids", double.NegativeInfinity)]
        public void InvalidTimeStopsMasterLoad(string key, double seconds)
        {
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(WithTime(key, seconds)));
            StringAssert.Contains("Time must be finite and > 0", error.Message);
        }

        [TestCase("fuelItems")]
        [TestCase("fuelFluids")]
        public void PositiveSubTickTimeRemainsValid(string key)
        {
            Assert.DoesNotThrow(() => MasterHolder.Load(WithTime(key, 0.01)));
        }

        private static MasterJsonFileContainer WithTime(string key, double seconds)
        {
            // 通常のマスタ入口へ変更済みJSONを渡す
            // Pass modified JSON through the normal master entry point
            var configs = ReadConfigs();
            var file = new JsonFileName("blocks");
            var json = JObject.Parse(configs[0].JsonContents[file]);
            var block = json["data"].First(b => b["blockParam"]?[key] is JArray fuels && 0 < fuels.Count);
            block["blockParam"][key][0]["time"] = seconds;
            configs[0].JsonContents[file] = json.ToString();
            return new MasterJsonFileContainer(configs);
        }

        private static System.Collections.Generic.List<MasterJsonContents> ReadConfigs()
        {
            return ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
        }
    }
}
