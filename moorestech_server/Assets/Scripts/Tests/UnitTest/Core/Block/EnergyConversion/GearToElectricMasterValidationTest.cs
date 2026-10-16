using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block.EnergyConversion
{
    public class GearToElectricMasterValidationTest
    {
        private MasterJsonFileContainer _original;
        private Guid _generatorGuid;

        [SetUp]
        public void Setup()
        {
            // 実テストModを毎回独立に読み、変更前のマスタを保持する
            // Read the actual test mod independently and keep the unchanged masters
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            _original = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resource));
            MasterHolder.Load(_original);
            _generatorGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockGuid;
        }

        [TearDown]
        public void RestoreMaster()
        {
            MasterHolder.Load(_original);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCapacityRejectsActualMasterLoad(float capacity)
        {
            var container = CreateWithCapacity(capacity);
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(container));
            StringAssert.Contains("maxGeneratedPower must be finite and > 0", error.Message);
            StringAssert.Contains("[BlockMaster] Name:", error.Message);
        }

        [TestCase(0.1f)]
        [TestCase(100f)]
        public void PositiveFiniteCapacityLoads(float capacity)
        {
            Assert.DoesNotThrow(() => MasterHolder.Load(CreateWithCapacity(capacity)));
            var param = (Mooresmaster.Model.BlocksModule.GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(_generatorGuid).BlockParam;
            Assert.AreEqual(capacity, param.MaxGeneratedPower);
        }

        private MasterJsonFileContainer CreateWithCapacity(float capacity)
        {
            // JSON境界から容量を差し替え、通常ロードの検証器まで通す
            // Replace capacity at the JSON boundary and run the normal loader's validator
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var configs = ModJsonStringLoader.GetMasterString(resource);
            var fileName = new JsonFileName("blocks");
            var json = JObject.Parse(configs[0].JsonContents[fileName]);
            var generator = json["data"].Single(block => (string)block["blockGuid"] == _generatorGuid.ToString());
            generator["blockParam"]["maxGeneratedPower"] = capacity;
            configs[0].JsonContents[fileName] = json.ToString();
            return new MasterJsonFileContainer(configs);
        }
    }
}
