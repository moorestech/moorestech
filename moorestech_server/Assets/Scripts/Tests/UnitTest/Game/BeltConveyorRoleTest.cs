using System.IO;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Interface.Extension;
using Game.Construction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// ファミリーのロール解決と、坂だけを直線代表へ寄せる財布・解放キーを検証する
    /// Verifies family role resolution and that only slopes normalize to the straight representative for wallet/unlock
    /// </summary>
    public class BeltConveyorRoleTest
    {
        private const string NonBeltBlockGuid = "00000000-0000-0000-0000-000000000002";

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 分岐器を含む4ロールを往復解決できる()
        {
            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);

            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.GearBeltConveyor, out var straightRole));
            Assert.AreEqual(BeltConveyorRole.Straight, straightRole);
            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.TestGearBeltConveyorUp, out var upRole));
            Assert.AreEqual(BeltConveyorRole.Up, upRole);
            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.TestGearBeltConveyorDown, out var downRole));
            Assert.AreEqual(BeltConveyorRole.Down, downRole);
            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.GearBeltConveyorSplitter, out var splitterRole));
            Assert.AreEqual(BeltConveyorRole.Splitter, splitterRole);

            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Straight, out var straightId));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, straightId);
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Up, out var upId));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, upId);
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Down, out var downId));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, downId);
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Splitter, out var splitterId));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, splitterId);

            // ファミリー外はロールを持たない
            // Non-members have no role
            Assert.IsFalse(family.TryGetRole(ForUnitTestModBlockId.MachineId, out _));
        }

        [Test]
        public void 坂を持たないファミリーは坂ロールの解決がfalseになる()
        {
            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.SmallGearBeltConveyor, out var family);

            Assert.IsFalse(family.TryGetBlockIdOfRole(BeltConveyorRole.Up, out _));
            Assert.IsFalse(family.TryGetBlockIdOfRole(BeltConveyorRole.Down, out _));
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Splitter, out var splitterId));
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, splitterId);
        }

        [Test]
        public void 財布キーは坂だけ直線へ寄り分岐器は自身のまま()
        {
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorDown));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyorSplitter));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 解放元は坂だけ直線へ寄り分岐器は自身のまま()
        {
            var map = new BeltConveyorPlacementUnlockSourceMap();
            var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
            var straightGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
            var splitterGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyorSplitter).BlockGuid;

            Assert.AreEqual(straightGuid, map.ResolveUnlockSourceId(upGuid));
            Assert.AreEqual(straightGuid, map.ResolveUnlockSourceId(straightGuid));
            Assert.AreEqual(splitterGuid, map.ResolveUnlockSourceId(splitterGuid));
        }

        [Test]
        public void 非ベルト型は分岐器ロールにできない()
        {
            var blocksJToken = LoadBlocksJson();
            blocksJToken["beltConveyorFamilies"][2]["splitterBlockGuid"] = NonBeltBlockGuid;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("is not a belt block", logs);
        }

        [Test]
        public void 分岐器は直線とコストが違っても検証エラーにならない()
        {
            var blocksJToken = LoadBlocksJson();
            var splitterGuid = blocksJToken["beltConveyorFamilies"][2]["splitterBlockGuid"].Value<string>();
            FindBlock(blocksJToken, splitterGuid)["placementsPerCost"] = 7;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.DoesNotContain("placementsPerCost must match", logs);
            StringAssert.DoesNotContain("requiredItems must match", logs);
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
    }
}
