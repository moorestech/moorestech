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
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());

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
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());

            // 90度回転で3x1x2が2x1x3へ
            // Rotating the master's 3x1x2 occupied extent yields 2x1x3
            Assert.AreEqual(new Vector3Int(3, 1, 2), BlueprintFootprintCalculator.CalcSize(blueprint, 0));
            Assert.AreEqual(new Vector3Int(2, 1, 3), BlueprintFootprintCalculator.CalcSize(blueprint, 1));
        }

        [Test]
        public void 最小角への正規化で複数ブロックの外形寸法は変わらないTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MultiBlockGeneratorId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("shifted", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(-7, 3, 9), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(-4, 4, 9), machineGuid, (int)BlockDirection.East, new Dictionary<string, string>()),
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());

            // 元オフセットの平行移動に関係なく外形の軸だけが入れ替わる
            // Rotation swaps extent axes independently of the original offset translation
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var expected = rotation % 2 == 0 ? new Vector3Int(5, 2, 3) : new Vector3Int(3, 2, 5);
                Assert.AreEqual(expected, BlueprintFootprintCalculator.CalcSize(blueprint, rotation), $"rotation={rotation}");
            }
        }

        [Test]
        public void UnresolvableBlueprintFoldsToOneCellTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = new BlueprintJsonObject("missing", new List<BlueprintBlockJsonObject>
            {
                new(Vector3Int.zero, Guid.NewGuid().ToString(), (int)BlockDirection.North, new Dictionary<string, string>()),
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());

            Assert.AreEqual(Vector3Int.one, BlueprintFootprintCalculator.CalcSize(blueprint, 0));
        }
    }
}
