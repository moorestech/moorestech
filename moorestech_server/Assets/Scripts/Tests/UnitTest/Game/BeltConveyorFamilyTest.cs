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
        [Test]
        public void 歯車ベルト系ブロックはファミリー解決できる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out _));
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor3, out _));
            // up/downバリアントもファミリー所属
            // Up/down slope variants also belong to the family
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.TestGearBeltConveyorUp, out _));
            // 非ベルトブロックは解決されない
            // Non-belt blocks are not resolved
            Assert.IsFalse(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.MachineId, out _));
        }

        [Test]
        public void 代表ブロックと長さ降順バリアントが正しい()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor3, out var family);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, family.RepresentativeBlockId);

            // 長さはblockSize.z導出・降順に並ぶ
            // Lengths derive from blockSize.z, sorted descending
            var variants = family.StraightVariantsDesc;
            Assert.AreEqual(3, variants.Count);
            Assert.AreEqual(3, variants[0].length);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor3, variants[0].blockId);
            Assert.AreEqual(1, variants[2].length);

            // 斜面はUp/DownBlockGuidから解決される
            // Slopes resolve from Up/DownBlockGuid
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, family.UpBlockId);
        }

        [Test]
        public void 隠しバリアント判定は代表のみfalse()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);

            Assert.IsTrue(family.IsHiddenVariant(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.IsTrue(family.IsHiddenVariant(ForUnitTestModBlockId.GearBeltConveyor3));
            Assert.IsFalse(family.IsHiddenVariant(ForUnitTestModBlockId.GearBeltConveyor));
        }

        [Test]
        public void 多重所属エラーから長さ1不足エラーを連鎖させない()
        {
            // 第1族の長さ1を第2族へ重複登録する
            // Register family one's length-1 block in family two
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            var firstBlockGuid = blocksJToken["beltConveyorFamilies"][0]["straightBlocks"][0]["blockGuid"].Value<string>();
            blocksJToken["beltConveyorFamilies"][1]["straightBlocks"][0]["blockGuid"] = firstBlockGuid;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("belongs to more than one family", logs);
            StringAssert.DoesNotContain("must contain exactly one length-1 straight block", logs);
        }

        [Test]
        public void 非ベルト型は長さ1代表として数えない()
        {
            // 第1族の長さ1を非ベルト型へ変更する
            // Replace family one's length-1 block with a non-belt block
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            blocksJToken["beltConveyorFamilies"][0]["straightBlocks"][0]["blockGuid"] = "00000000-0000-0000-0000-000000000002";

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("is not a belt block", logs);
            StringAssert.Contains("must contain exactly one length-1 straight block", logs);
        }
    }
}
