using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintCopyTargetRuleTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void RailFamilyIsExcludedTest()
        {
            var rail = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainRail);
            var chest = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);

            // レールだけを除外し、通常ブロックはコピー可能にする
            // Exclude rail-family blocks while retaining ordinary blocks
            Assert.IsFalse(BlueprintCopyTargetRule.IsCopyTarget(rail));
            Assert.IsTrue(BlueprintCopyTargetRule.IsCopyTarget(chest));
        }

        [Test]
        public void OneOccupiedCellInsideBoxIncludesMultiCellBlockTest()
        {
            var blockId = ForUnitTestModBlockId.MultiBlockGeneratorId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var position = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, master.BlockSize);
            Assert.AreNotEqual(position.MinPos, position.MaxPos);

            // 最大端の1セルだけに範囲が重なっても対象に含める
            // Include the block when only its maximum occupied cell intersects
            Assert.IsTrue(BlueprintCopyTargetRule.IntersectsBox(position, position.MaxPos, position.MaxPos));
            var outside = position.MaxPos + Vector3Int.right;
            Assert.IsFalse(BlueprintCopyTargetRule.IntersectsBox(position, outside, outside));
        }
    }
}
