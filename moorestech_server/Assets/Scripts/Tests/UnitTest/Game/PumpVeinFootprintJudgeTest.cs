using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ポンプの汲み上げ対象判定は採掘機と同じ底面XZ重なりで、Yと原点セルは見ない
    ///     The pump target rule is the miner's footprint XZ overlap; neither Y nor the origin cell matters
    /// </summary>
    public class PumpVeinFootprintJudgeTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Steam = new(2);
        private static readonly Vector3Int VeinMin = new(10, 0, 10);
        private static readonly Vector3Int VeinMax = new(12, 0, 12);

        [Test]
        public void 原点が鉱脈外でも底面の端が鉱脈にXZで掛かれば対象になる()
        {
            // 3x1x3 北向き原点(8,0,8): x:8..10 z:8..10 で鉱脈角(10,10)に掛かる
            // 3x1x3 facing north at (8,0,8) spans x:8..10 z:8..10 and touches the vein corner (10,10)
            var footprint = new BlockPositionInfo(new Vector3Int(8, 0, 8), BlockDirection.North, new Vector3Int(3, 1, 3));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsTrue(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void 鉱脈AABBのYから外れてもXZが重なれば対象になる()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(11, 7, 11), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsTrue(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void 隣接だけでは対象にならない()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(13, 0, 10), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsFalse(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void generateFluidに無い流体の鉱脈は重なっても対象にならない()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(11, 0, 11), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsFalse(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Steam));
        }
    }
}
