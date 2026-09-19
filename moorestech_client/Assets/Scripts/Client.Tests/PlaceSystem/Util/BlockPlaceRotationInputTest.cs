using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Util
{
    /// <summary>
    ///     設置向きの回転が水平/上下の片方だけ掛かることを検証
    ///     Verifies that a rotation applies exactly one of the horizontal or vertical steps
    /// </summary>
    public class BlockPlaceRotationInputTest
    {
        [TestCase(BlockDirection.North, BlockDirection.East)]
        [TestCase(BlockDirection.West, BlockDirection.North)]
        [TestCase(BlockDirection.UpNorth, BlockDirection.UpEast)]
        public void 修飾なしの回転は水平に90度だけ回る(BlockDirection current, BlockDirection expected)
        {
            Assert.AreEqual(expected, BlockPlaceRotationInput.Rotate(current, false));
        }

        // 旧実装は水平回転の後に上下回転も掛け、North から UpEast へ飛んでいた
        // The old code applied the horizontal step and then the vertical one, jumping from North to UpEast
        [TestCase(BlockDirection.North, BlockDirection.UpNorth)]
        [TestCase(BlockDirection.East, BlockDirection.UpEast)]
        [TestCase(BlockDirection.UpSouth, BlockDirection.DownSouth)]
        [TestCase(BlockDirection.DownWest, BlockDirection.West)]
        public void Shift付きの回転は上下だけ回り水平には回らない(BlockDirection current, BlockDirection expected)
        {
            Assert.AreEqual(expected, BlockPlaceRotationInput.Rotate(current, true));
        }
    }
}
