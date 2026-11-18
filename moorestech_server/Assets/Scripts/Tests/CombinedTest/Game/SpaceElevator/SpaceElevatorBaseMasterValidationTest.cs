using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorBaseMasterValidationTest
    {
        [SetUp] public void Setup() => SpaceElevatorTestUtil.CreateServer();
        [TearDown] public void RestoreMaster() => SpaceElevatorTestUtil.CreateServer();

        [TestCase("capacity")]
        [TestCase("cable")]
        [TestCase("satellite")]
        [TestCase("duplicate")]
        public void MasterLoadAloneRejectsInvalidOrbitalConfiguration(string variant)
        {
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemKey = new JsonFileName("items");
            var blockKey = new JsonFileName("blocks");
            var items = JObject.Parse(configs[0].JsonContents[itemKey]);
            var blocks = JObject.Parse(configs[0].JsonContents[blockKey]);
            var satellite = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorSatellite);
            var cable = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorCable);
            var row = items["orbitalPayloads"].Single(p => (Guid)p["payloadItemGuid"] == satellite);

            // 生マスタ入口だけで不正を拒否
            // Verify rejection at the raw master entry without constructing game services
            if (variant == "capacity")
            {
                row["arrivalOutputs"][0]["count"] = 101;
                foreach (var block in blocks["data"].Where(b => (string)b["blockType"] == "RocketLaunchPad"))
                    block["blockParam"]["outputSlotCount"] = 100;
            }
            if (variant == "satellite") row.Remove();
            if (variant == "duplicate")
            {
                foreach (var block in blocks["data"].Where(b => (string)b["blockType"] == "SpaceElevatorBase"))
                    block["blockParam"]["carriedPayloadItemGuids"][1] = block["blockParam"]["carriedPayloadItemGuids"][0].DeepClone();
            }
            if (variant == "cable") items["orbitalPayloads"].Single(p => (Guid)p["payloadItemGuid"] == cable).Remove();
            configs[0].JsonContents[itemKey] = items.ToString();
            configs[0].JsonContents[blockKey] = blocks.ToString();
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(new MasterJsonFileContainer(configs)));
            var expected = variant == "capacity" ? "outputSlotCount 1 cannot hold" : variant == "duplicate" ? "has duplicate carriedPayloadItemGuid" : "missing from orbitalPayloads";
            StringAssert.Contains(expected, error.Message);
        }
    }
}
