using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class MinerDrillLocalPositionValidationTest
    {
        [Test]
        public void ドリル位置がブロックサイズの外なら検証エラーになる()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(path));

            // 2x1x3の採掘機に、footprintの外(x=2)を指すドリル位置を入れる
            // Point the 2x1x3 miner's drill at x=2, one cell outside its footprint
            foreach (var block in blocksJToken["data"])
            {
                if (block["name"]?.ToString() != "TestOffsetDrillMiner") continue;
                block["blockParam"]["drillLocalPosition"] = new JArray(2, 0, 2);
            }

            BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var logs);

            StringAssert.Contains("DrillLocalPosition", logs);
        }

        [Test]
        public void 素のマスタはドリル位置の検証を通る()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(path));

            BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var logs);

            StringAssert.DoesNotContain("DrillLocalPosition", logs);
        }
    }
}
