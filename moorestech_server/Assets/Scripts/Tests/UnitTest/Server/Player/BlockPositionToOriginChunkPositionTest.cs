using NUnit.Framework;
using Server.Protocol.PacketResponse.Const;
using UnityEngine;

namespace Tests.UnitTest.Server.Player
{
    public class BlockPositionToOriginChunkPositionTest
    {
        //x yがプラスの時
        [TestCase(0, 0, 0, 0)]
        [TestCase(10, 0, 0, 0)]
        [TestCase(0, 25, 0, 20)]
        [TestCase(19, 39, 0, 20)]
        [TestCase(20, 40, 20, 40)]
        [TestCase(105, 415, 100, 400)]
        //xがプラス、yがマイナスの時
        [TestCase(0, -25, 0, -40)]
        [TestCase(105, -415, 100, -420)]
        //xがマイナス、yがプラスの時
        [TestCase(-10, 0, -20, 0)]
        [TestCase(-105, 415, -120, 400)]
        //xがマイナス、yがマイナスの時
        [TestCase(-1, -21, -20, -40)]
        [TestCase(-20, -40, -20, -40)]
        [TestCase(-21, -41, -40, -60)]
        [TestCase(-10, -25, -20, -40)]
        [TestCase(-105, -415, -120, -420)]
        public void ConvertTest(int blockX, int blockY, int expectedChunkX, int expectedChunkY)
        {
            var pos = ChunkResponseConst.BlockPositionToChunkOriginPosition(new Vector3Int(blockX, blockY));
            Assert.AreEqual(expectedChunkX, pos.x);
            Assert.AreEqual(expectedChunkY, pos.y);
        }
    }
}