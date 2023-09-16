using System;
using System.Collections.Generic;
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
            var cList = p.GetResponseChunkCoordinates(new Coordinate(0, 0));

            var ans = new List<Coordinate>();
            if (ChunkResponseConst.ChunkSize != 20 || ChunkResponseConst.PlayerVisibleRangeChunk != 5)
            {
                Assert.Fail("Changed const?");
            }

            for (int i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                {
                    ans.Add(new Coordinate(i, j));
                }
            }

            foreach (var a in ans)
            {
                Assert.True(cList.Contains(a));
            }

            //2回目は何も返さない
            cList = p.GetResponseChunkCoordinates(new Coordinate(0, 0));
            Assert.AreEqual(0, cList.Count);
        }


        //初期座標がマイナスの時のテスト
        [Test]
        public void OneMinusCoordinateResponseTest()
        {
            
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new Coordinate(-10, 0));

            var ans = new List<Coordinate>();
            if (ChunkResponseConst.ChunkSize != 20 || ChunkResponseConst.PlayerVisibleRangeChunk != 5)
            {
                Assert.Fail("Changed const?");
            }

            for (int i = -60; i <= 20; i += ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                {
                    ans.Add(new Coordinate(i, j));
                }
            }

            foreach (var a in ans)
            {
                Assert.True(cList.Contains(a));
            }

            //2回目は何も返さない
            cList = p.GetResponseChunkCoordinates(new Coordinate(-10, 0));
            Assert.AreEqual(0, cList.Count);
        }

        [Test]
        public void ShiftSideToCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new Coordinate(0, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new Coordinate(25, 0));
            var ans = new List<Coordinate>();
            for (int i = -2; i < 3; i++)
            {
                ans.Add(new Coordinate(60, i * ChunkResponseConst.ChunkSize));
            }

            foreach (var a in ans)
            {
                Assert.True(cList.Contains(a));
            }
        }

        [Test]
        public void ShiftSideAndUpToCoordinateResponseTest()
        {
            var p = new PlayerCoordinateToResponse();
            //1回目は全てを返す
            var cList = p.GetResponseChunkCoordinates(new Coordinate(0, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new Coordinate(25, 25));
            var ans = new List<Coordinate>();
            for (int i = -1; i < 4; i++)
            {
                ans.Add(new Coordinate(i * ChunkResponseConst.ChunkSize, 60));
            }

            ans.Add(new Coordinate(-20, 60));
            ans.Add(new Coordinate(0, 60));
            ans.Add(new Coordinate(20, 60));
            ans.Add(new Coordinate(40, 60));

            foreach (var a in ans)
            {
                Assert.True(cList.Contains(a));
            }
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
            var cList = p.GetResponseChunkCoordinates(new Coordinate(startX, 0));
            Assert.AreEqual(cList.Count,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk);

            //2回目1チャンクx分を増加させる
            cList = p.GetResponseChunkCoordinates(new Coordinate(endX, 0));
            var ans = new List<Coordinate>();
            for (int i = -2; i < 3; i++)
            {
                ans.Add(new Coordinate(getChunkX, i * ChunkResponseConst.ChunkSize));
            }

            foreach (var a in ans)
            {
                Assert.True(cList.Contains(a));
            }
        }
    }
}