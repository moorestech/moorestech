using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util;

namespace Test.UnitTest.Server.Player
{
    public class BlockPositionToOriginChunkPositionTest
    {
        //x yがプラスの時
        [TestCase(0,0,0,0)]
        [TestCase(10,0,0,0)]
        [TestCase(0,25,0,20)]
        [TestCase(105,415,100,400)]
        //xがプラス、yがマイナスの時
        [TestCase(0,-25,0,-40)]
        [TestCase(105,-415,100,-420)]
        //xがマイナス、yがプラスの時
        [TestCase(-10,0,-20,0)]
        [TestCase(-105,415,-120,400)]
        //xがマイナス、yがマイナスの時
        [TestCase(-10,-25,-20,-40)]
        [TestCase(-105,-415,-120,-420)]
        public void ConvertTest(int blockX,int blockY,int  expectedChunkX,int expectedChunkY)
        {
            var (x,y) = new BlockPositionToOriginChunkPosition().Convert(blockX, blockY);
            Assert.AreEqual(expectedChunkX, x);
            Assert.AreEqual(expectedChunkY, y);
        }
    }
}