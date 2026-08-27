using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Context;
using Tests.Module.TestMod;
using Server.Protocol.PacketResponse;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    // コンベア分岐削除後(Task 6)の直線設置ロジックを検証する
    // Verify the straight-line placement logic left after Task 6 removed the conveyor branch
    public class CommonBlockPlacePointCalculatorTest
    {
        // 常にDirection=指定値・VerticalDirection=Horizontalで設置情報を返すこと
        // Placement info always carries the given Direction and a Horizontal VerticalDirection
        [Test]
        public void StraightLine_AlwaysHorizontal_WithGivenDirection()
        {
            var blockMasterElement = MakeBlock(Vector3Int.one);

            var actual = CommonBlockPlacePointCalculator.CalculatePoint(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), BlockDirection.East,
                blockMasterElement, (_, _) => true, out _);

            Assert.AreEqual(3, actual.Count);
            foreach (var info in actual)
            {
                Assert.AreEqual(BlockDirection.East, info.Direction);
                Assert.AreEqual(BlockVerticalDirection.Horizontal, info.VerticalDirection);
                Assert.IsTrue(info.Placeable);
            }
        }

        // blockSize分だけ間隔を空けて配置点を刻むこと
        // Placement points are spaced by blockSize
        [Test]
        public void MultiCellBlockSize_StepsByBlockSize()
        {
            var blockMasterElement = MakeBlock(new Vector3Int(2, 1, 1));

            var actual = CommonBlockPlacePointCalculator.CalculatePoint(
                new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0), BlockDirection.North,
                blockMasterElement, (_, _) => true, out _);

            Assert.AreEqual(3, actual.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), actual[0].Position);
            Assert.AreEqual(new Vector3Int(2, 0, 0), actual[1].Position);
            Assert.AreEqual(new Vector3Int(4, 0, 0), actual[2].Position);
        }

        // isNotExistBlockがfalseを返す位置だけPlaceable=falseになること
        // Only the position where isNotExistBlock returns false becomes unplaceable
        [Test]
        public void IsNotExistBlock_False_MarksPositionUnplaceable()
        {
            var blockMasterElement = MakeBlock(Vector3Int.one);
            var occupied = new Vector3Int(1, 0, 0);

            var actual = CommonBlockPlacePointCalculator.CalculatePoint(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), BlockDirection.East,
                blockMasterElement, (info, _) => info.Position != occupied, out var blockCauses);

            Assert.IsTrue(actual[0].Placeable);
            Assert.IsFalse(actual[1].Placeable);
            Assert.IsTrue(actual[2].Placeable);

            // 不可原因の列はPlaceInfo列と同じ添字で並走する
            // The block cause column runs alongside the PlaceInfo list on the same index
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[0]);
            Assert.AreEqual(PlacementBlockCause.ExistingBlock, blockCauses[1]);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[2]);
        }


        // 取り直すと古い不可フラグが解除される
        // Re-checking clears a stale not-placeable flag
        [Test]
        public void RecalculateExistingBlockCauses_ClearsStaleCause()
        {
            // 重なり判定がマスタを引くため先にロード
            // The overlap check reads MasterHolder, so load the masters first
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var dataStoreObject = new GameObject("BlockGameObjectDataStore");
            var calculator = new CommonBlockPlacePointCalculator(dataStoreObject.AddComponent<BlockGameObjectDataStore>());

            var blockElement = MasterHolder.BlockMaster.Blocks.Data[0];
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(0, 5, 0),
                    Direction = BlockDirection.North,
                    BlockId = MasterHolder.BlockMaster.GetBlockId(blockElement.BlockGuid),
                    Placeable = false,
                },
            };
            var blockCauses = new List<PlacementBlockCause> { PlacementBlockCause.ExistingBlock };

            calculator.RecalculateExistingBlockCauses(placeInfos, blockElement, blockCauses);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[0]);

            UnityEngine.Object.DestroyImmediate(dataStoreObject);
        }

        private static BlockMasterElement MakeBlock(Vector3Int blockSize)
        {
            return new BlockMasterElement(
                0,
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                                null,
                1, // placementsPerCost
                null,
                "テスト",
                "テスト",
                0,
                false,
                blockSize,
                null,
                null,
                null
            );
        }
    }
}
