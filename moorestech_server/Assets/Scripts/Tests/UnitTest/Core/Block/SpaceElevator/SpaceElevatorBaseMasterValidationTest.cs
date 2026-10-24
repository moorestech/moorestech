using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block.SpaceElevator
{
    public class SpaceElevatorBaseMasterValidationTest
    {
        private const string BaseGuid = "5e1ea7a0-0001-4000-8000-000000000001";
        private const string CableGuid = "5e1ea7a0-0002-4000-8000-000000000001";
        private MasterJsonFileContainer _original;

        [SetUp]
        public void Setup()
        {
            // 実テストModを毎回独立に読み、変更前のマスタを保持する
            // Read the actual test mod independently and keep the unchanged masters
            _original = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(CreateResource()));
            MasterHolder.Load(_original);
        }

        [TearDown]
        public void RestoreMaster()
        {
            MasterHolder.Load(_original);
        }

        [Test]
        public void テストmodの拠点はそのまま読める()
        {
            var param = (Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase).BlockParam;
            Assert.AreEqual(100, param.CablesPerElevator);
        }

        [Test]
        public void 運ぶ積荷にケーブルを入れるとマスタ読込で止まる()
        {
            var container = CreateWith(param => param["carriedPayloadItemGuids"] = new JArray(CableGuid));
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(container));
            StringAssert.Contains("must not carry its own cable item", error.Message);
        }

        [TestCase("cablesPerElevator", 0, "cablesPerElevator must be >= 1")]
        [TestCase("climbSeconds", 0, "climbSeconds must be finite and >= 1")]
        [TestCase("requiredPower", 0, "requiredPower must be finite and > 0")]
        [TestCase("outputSlotCount", 0, "outputSlotCount must be >= 1")]
        public void 数値が範囲外だとマスタ読込で止まる(string key, int value, string message)
        {
            var container = CreateWith(param => param[key] = value);
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(container));
            StringAssert.Contains(message, error.Message);
        }

        private static MasterJsonFileContainer CreateWith(Action<JObject> edit)
        {
            // 拠点JSON変更を通常ロードで検証
            // Verify edited base JSON through the normal loader and its validator
            var configs = ModJsonStringLoader.GetMasterString(CreateResource());
            var fileName = new JsonFileName("blocks");
            var json = JObject.Parse(configs[0].JsonContents[fileName]);
            var elevatorBase = json["data"].Single(block => (string)block["blockGuid"] == BaseGuid);
            edit((JObject)elevatorBase["blockParam"]);
            configs[0].JsonContents[fileName] = json.ToString();
            return new MasterJsonFileContainer(configs);
        }

        private static ModsResource CreateResource()
        {
            return new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
        }
    }
}
