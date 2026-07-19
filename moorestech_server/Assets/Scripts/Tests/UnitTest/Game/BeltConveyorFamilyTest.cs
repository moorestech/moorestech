using System.IO;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Interface.Extension;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class BeltConveyorFamilyTest
    {
        private const string NonBeltBlockGuid = "00000000-0000-0000-0000-000000000002";

        [Test]
        public void 歯車ベルト系ブロックは単一直線ファミリーとして解決できる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 直線と坂を同じファミリーへ解決する
            // Resolve the straight and slope blocks to the same family
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family));
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.TestGearBeltConveyorUp, out _));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, family.StraightBlockId);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, family.UpBlockId);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, family.DownBlockId);

            Assert.IsFalse(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.MachineId, out _));
        }

        [Test]
        public void 坂ブロックだけを非直線メンバーとして判定する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);

            Assert.IsTrue(family.IsSlopeBlock(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.IsTrue(family.IsSlopeBlock(ForUnitTestModBlockId.TestGearBeltConveyorDown));
            Assert.IsFalse(family.IsSlopeBlock(ForUnitTestModBlockId.GearBeltConveyor));
        }

        [Test]
        public void ファミリーメンバーが2セルなら検証エラーになる()
        {
            var blocksJToken = LoadBlocksJson();
            var straightBlockGuid = blocksJToken["beltConveyorFamilies"][0]["straightBlockGuid"].Value<string>();
            var straightBlock = FindBlock(blocksJToken, straightBlockGuid);
            straightBlock["blockSize"] = new JArray(1, 1, 2);

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("blockSize must be [1,1,1]", logs);
        }

        [Test]
        public void 多重所属エラーから未所属エラーを連鎖させない()
        {
            var blocksJToken = LoadBlocksJson();
            var firstBlockGuid = blocksJToken["beltConveyorFamilies"][0]["straightBlockGuid"].Value<string>();
            var displacedBlockGuid = blocksJToken["beltConveyorFamilies"][1]["straightBlockGuid"].Value<string>();
            blocksJToken["beltConveyorFamilies"][1]["straightBlockGuid"] = firstBlockGuid;
            AddSingleBlockFamily(blocksJToken, displacedBlockGuid);

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("belongs to more than one family", logs);
            StringAssert.DoesNotContain("belongs to no beltConveyorFamily", logs);
        }

        [Test]
        public void 非ベルト型はファミリー直線ブロックにできない()
        {
            var blocksJToken = LoadBlocksJson();
            var displacedBlockGuid = blocksJToken["beltConveyorFamilies"][0]["straightBlockGuid"].Value<string>();
            blocksJToken["beltConveyorFamilies"][0]["straightBlockGuid"] = NonBeltBlockGuid;
            AddSingleBlockFamily(blocksJToken, displacedBlockGuid);

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("is not a belt block", logs);
            StringAssert.DoesNotContain("belongs to no beltConveyorFamily", logs);
        }

        private static JToken LoadBlocksJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(path));
        }

        private static JToken FindBlock(JToken blocksJToken, string blockGuid)
        {
            foreach (var block in blocksJToken["data"])
            {
                if (block["blockGuid"].Value<string>() == blockGuid) return block;
            }

            Assert.Fail($"Block not found: {blockGuid}");
            return null;
        }

        private static void AddSingleBlockFamily(JToken blocksJToken, string blockGuid)
        {
            var family = new JObject { ["straightBlockGuid"] = blockGuid };
            ((JArray)blocksJToken["beltConveyorFamilies"]).Add(family);
        }
    }
}
