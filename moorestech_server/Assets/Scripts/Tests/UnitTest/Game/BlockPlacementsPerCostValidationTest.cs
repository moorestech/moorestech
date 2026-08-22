using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class BlockPlacementsPerCostValidationTest
    {
        [Test]
        public void placementsPerCostが0以下なら検証エラーになる()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(path));
            blocksJToken["data"][0]["placementsPerCost"] = 0;

            BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var logs);

            StringAssert.Contains("invalid PlacementsPerCost:0", logs);
        }
    }
}
