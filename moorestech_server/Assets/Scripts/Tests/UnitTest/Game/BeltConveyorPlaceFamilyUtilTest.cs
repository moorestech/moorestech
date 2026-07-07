using Core.Master;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class BeltConveyorPlaceFamilyUtilTest
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

            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor3, out var param);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(param));

            var variants = BeltConveyorPlaceFamilyUtil.GetStraightVariantsDesc(param);
            Assert.AreEqual(3, variants.Count);
            Assert.AreEqual(3, variants[0].length);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor3, variants[0].blockId);
            Assert.AreEqual(1, variants[2].length);
        }

        [Test]
        public void 隠しバリアント判定は代表のみfalse()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
            var repGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
            var longGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor3).BlockGuid;

            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(upGuid));
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(longGuid));
            Assert.IsFalse(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(repGuid));
        }
    }
}
