#if NET6_0
using System.Collections.Generic;
using Core.Util;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Player;

namespace Test.UnitTest.Server.Player
{
    public class PlayerCoordinateToResponseTest
    {
        [Test]
        public void OneCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new CoreVector2Int(0, 0));

            var ans = new List<CoreVector2Int>();
            if (ChunkResponseConst.ChunkSize != 20 || ChunkResponseConst.PlayerVisibleRangeChunk != 5) Assert.Fail("Changed const?");

            for (var i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            for (var j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                ans.Add(new CoreVector2Int(i, j));

            foreach (var a in ans) Assert.True(cList.Contains(a));

            //2回目は何も返さない
            cList = p.GetResponseChunkCoordinates(new CoreVector2Int(0, 0));
            Assert.AreEqual(0, cList.Count);
        }


        //初期座標がマイナスの時のテスト
        [Test]
        public void OneMinusCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new CoreVector2Int(-10, 0));

            var ans = new List<CoreVector2Int>();
            if (ChunkResponseConst.ChunkSize != 20 || ChunkResponseConst.PlayerVisibleRangeChunk != 5) Assert.Fail("Changed const?");

            for (var i = -60; i <= 20; i += ChunkResponseConst.ChunkSize)
            for (var j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                ans.Add(new CoreVector2Int(i, j));

            foreach (var a in ans) Assert.True(cList.Contains(a));

            //2回目は何も返さない
            cList = p.GetResponseChunkCoordinates(new CoreVector2Int(-10, 0));
            Assert.AreEqual(0, cList.Count);
        }

        [Test]
        public void ShiftSideToCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new CoreVector2Int(0, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new CoreVector2Int(25, 0));
            var ans = new List<CoreVector2Int>();
            for (var i = -2; i < 3; i++) ans.Add(new CoreVector2Int(60, i * ChunkResponseConst.ChunkSize));

            foreach (var a in ans) Assert.True(cList.Contains(a));
        }

        [Test]
        public void ShiftSideAndUpToCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new CoreVector2Int(0, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new CoreVector2Int(25, 25));
            var ans = new List<CoreVector2Int>();
            for (var i = -1; i < 4; i++) ans.Add(new CoreVector2Int(i * ChunkResponseConst.ChunkSize, 60));

            ans.Add(new CoreVector2Int(-20, 60));
            ans.Add(new CoreVector2Int(0, 60));
            ans.Add(new CoreVector2Int(20, 60));
            ans.Add(new CoreVector2Int(40, 60));

            foreach (var a in ans) Assert.True(cList.Contains(a));
        }


        //startXからendXまで移動した時にえられるx座標のチャンクのデータを返す
        [TestCase(0, 25, 60)]
        [TestCase(25, 45, 80)]
        [TestCase(1000, 1020, 1060)]
        [TestCase(0, -25, -60)]
        [TestCase(-25, -45, -100)]
        [TestCase(-1000, -1020, -1060)]
        [TestCase(-25, 0, 40)]
        public void ShiftOneChunkXToCoordinateResponseTest(int startX, int endX, int getChunkX)
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new CoreVector2Int(startX, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new CoreVector2Int(endX, 0));
            var ans = new List<CoreVector2Int>();
            for (var i = -2; i < 3; i++) ans.Add(new CoreVector2Int(getChunkX, i * ChunkResponseConst.ChunkSize));

            foreach (var a in ans) Assert.True(cList.Contains(a));
        }
    }
}
#endif