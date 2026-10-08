using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopyRangeCounterTest
    {
        private static BlockMasterElement MakeBlock(string blockType, Vector3Int size)
        {
            return new BlockMasterElement(0, Guid.Empty, "TestBlock", blockType, null, 1, null, "テスト", "テスト", 0, false, size, null, false, null);
        }

        [Test]
        public void 一部でも交差すれば数え_レール系は数えない()
        {
            var chest = MakeBlock("Chest", Vector3Int.one);
            var kiln = MakeBlock("ElectricMachine", new Vector3Int(3, 2, 3));
            var rail = MakeBlock(BlockMasterElement.BlockTypeConst.TrainRail, Vector3Int.one);
            var blocks = new List<(BlockMasterElement, BlockPositionInfo)>
            {
                (chest, new BlockPositionInfo(new Vector3Int(2, 32, 2), BlockDirection.North, Vector3Int.one)),
                (kiln, new BlockPositionInfo(new Vector3Int(10, 32, 2), BlockDirection.North, new Vector3Int(3, 2, 3))),
                (chest, new BlockPositionInfo(new Vector3Int(100, 32, 100), BlockDirection.North, Vector3Int.one)),
                (rail, new BlockPositionInfo(new Vector3Int(3, 32, 3), BlockDirection.North, Vector3Int.one)),
            };

            // 占有セルの角だけ入る大型ブロックを含めて数える
            // Count a large block even if only one corner intersects
            Assert.AreEqual(2, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(0, 32, 0), new Vector3Int(11, 32, 4)));
            Assert.AreEqual(1, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(0, 33, 0), new Vector3Int(11, 33, 4)));
            Assert.AreEqual(0, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(3, 32, 2), new Vector3Int(4, 32, 4)));
        }
    }
}
