using Game.World.Interface.DataStore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.Protocol.PacketResponse.Util;

namespace Test.UnitTest.Server.Player
{
    [TestClass]
    public class BlockPositionToOriginChunkPositionTest
    {

        [TestMethod]
        public void ConvertTest()
        {
            //x yがプラスの時
            ConvertTest(0, 0, 0, 0);
            ConvertTest(10, 0, 0, 0);
            ConvertTest(0, 25, 0, 20);
            ConvertTest(19, 39, 0, 20);
            ConvertTest(20, 40, 20, 40);
            ConvertTest(105, 415, 100, 400);
            //xがプラス、yがマイナスの時
            ConvertTest(0, -25, 0, -40);
            ConvertTest(105, -415, 100, -420);
            //xがマイナス、yがプラスの時
            ConvertTest(-10, 0, -20, 0);
            ConvertTest(-105, 415, -120, 400);
            //xがマイナス、yがマイナスの時
            ConvertTest(-1, -21, -20, -40);
            ConvertTest(-20, -40, -20, -40);
            ConvertTest(-21, -41, -40, -60);
            ConvertTest(-10, -25, -20, -40);
            ConvertTest(-105, -415, -120, -420);
        }
        
        public void ConvertTest(int blockX,int blockY,int  expectedChunkX,int expectedChunkY)
        {
            var (x,y) = new BlockPositionToOriginChunkPosition().Convert(blockX, blockY);
            Assert.AreEqual(expectedChunkX, x);
            Assert.AreEqual(expectedChunkY, y);
        }
    }
}