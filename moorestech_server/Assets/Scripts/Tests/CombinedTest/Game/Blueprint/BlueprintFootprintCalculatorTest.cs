using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintFootprintCalculatorTest
    {
        [Test]
        public void TwoChestsInLineFootprintTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("line", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(2, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            // 90度回すとX方向の幅がZ方向の幅へ移る
            // A quarter turn moves the X span onto Z
            Assert.AreEqual(new Vector3Int(3, 1, 1), BlueprintFootprintCalculator.CalcSize(blueprint, 0));
            Assert.AreEqual(new Vector3Int(1, 1, 3), BlueprintFootprintCalculator.CalcSize(blueprint, 1));
        }

        [Test]
        public void RotatedMultiCellBlockUsesMasterSizeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MultiBlockGeneratorId);
            Assert.AreEqual(new Vector3Int(3, 1, 2), master.BlockSize);
            var blueprint = new BlueprintJsonObject("multi", new List<BlueprintBlockJsonObject>
            {
                new(Vector3Int.zero, master.BlockGuid.ToString(), (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            // マスタの3x1x2占有外形が90度回転で2x1x3へ移る
            // Rotating the master's 3x1x2 occupied extent yields 2x1x3
            Assert.AreEqual(new Vector3Int(3, 1, 2), BlueprintFootprintCalculator.CalcSize(blueprint, 0));
            Assert.AreEqual(new Vector3Int(2, 1, 3), BlueprintFootprintCalculator.CalcSize(blueprint, 1));
        }

        [Test]
        public void UnresolvableBlueprintFoldsToOneCellTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = new BlueprintJsonObject("missing", new List<BlueprintBlockJsonObject>
            {
                new(Vector3Int.zero, Guid.NewGuid().ToString(), (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            Assert.AreEqual(Vector3Int.one, BlueprintFootprintCalculator.CalcSize(blueprint, 0));
        }
    }
}
