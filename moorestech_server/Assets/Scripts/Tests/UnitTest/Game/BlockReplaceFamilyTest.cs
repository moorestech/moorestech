using System.IO;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Interface.Extension;
using Mooresmaster.Loader.BuildMenuModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class BlockReplaceFamilyTest
    {
        private const string InvalidBlockGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        [Test]
        public void 同一ファミリー所属ブロックのみ相互リプレース可能と判定する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ベルト系同士は電気・歯車・分岐器をまたいで同一ファミリー
            // Belt-type blocks share one family across electric, gear, and splitter variants
            Assert.IsTrue(BlockReplaceFamilyUtil.IsSameReplaceFamily(ForUnitTestModBlockId.BeltConveyorId, ForUnitTestModBlockId.GearBeltConveyor));
            Assert.IsTrue(BlockReplaceFamilyUtil.IsSameReplaceFamily(ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.FilterSplitter));

            // ファミリー外ブロックはどの向きの組み合わせでも不可
            // Blocks outside the family are rejected in either direction
            Assert.IsFalse(BlockReplaceFamilyUtil.IsSameReplaceFamily(ForUnitTestModBlockId.BeltConveyorId, ForUnitTestModBlockId.MachineId));
            Assert.IsFalse(BlockReplaceFamilyUtil.IsSameReplaceFamily(ForUnitTestModBlockId.MachineId, ForUnitTestModBlockId.BeltConveyorId));
            Assert.IsFalse(BlockReplaceFamilyUtil.IsSameReplaceFamily(ForUnitTestModBlockId.MachineId, ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 実在しないブロックGUIDは検証エラーになる()
        {
            var buildMenuJToken = LoadBuildMenuJson();
            buildMenuJToken["replaceFamilies"][0]["targetBlocks"][0]["blockGuid"] = InvalidBlockGuid;

            var logs = Validate(buildMenuJToken);

            StringAssert.Contains("has invalid BlockGuid", logs);
        }

        [Test]
        public void 複数ファミリーへの重複所属は検証エラーになる()
        {
            var buildMenuJToken = LoadBuildMenuJson();
            var duplicatedBlockGuid = buildMenuJToken["replaceFamilies"][0]["targetBlocks"][0]["blockGuid"].Value<string>();
            var secondFamily = new JObject
            {
                ["familyName"] = "SecondFamily",
                ["targetBlocks"] = new JArray(new JObject { ["blockGuid"] = duplicatedBlockGuid }),
            };
            ((JArray)buildMenuJToken["replaceFamilies"]).Add(secondFamily);

            var logs = Validate(buildMenuJToken);

            StringAssert.Contains("is assigned to multiple replace families", logs);
        }

        private static JToken LoadBuildMenuJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "buildMenu.json");
            return JToken.Parse(File.ReadAllText(path));
        }

        private static string Validate(JToken buildMenuJToken)
        {
            var blocksPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocks = new BlockMaster(JToken.Parse(File.ReadAllText(blocksPath))).Blocks;
            return ReplaceFamilyValidator.Validate(blocks, BuildMenuLoader.Load(buildMenuJToken).ReplaceFamilies);
        }
    }
}
