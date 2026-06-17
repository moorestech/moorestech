using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests
{
    public class ConveyorOverpassRaiserTest
    {
        private static List<PlaceInfo> FlatHorizontalPath(int length)
        {
            // 始点から+X方向に水平に並ぶベルト列を作る
            // Build a horizontal belt row extending in +X from the origin.
            var list = new List<PlaceInfo>();
            for (var x = 0; x < length; x++)
            {
                list.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, 0, 0),
                    Direction = BlockDirection.East,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                });
            }
            return list;
        }

        [Test]
        public void NoObstacle_LeavesEverythingUnchanged()
        {
            var infos = FlatHorizontalPath(3);
            new ConveyorOverpassRaiser().Raise(infos, 2, _ => false);

            for (var x = 0; x < 3; x++)
            {
                Assert.AreEqual(new Vector3Int(x, 0, 0), infos[x].Position);
                Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[x].VerticalDirection);
                Assert.IsTrue(infos[x].Placeable);
            }
        }

        [Test]
        public void Height1ObstacleInMiddle_BuildsOverpass()
        {
            var infos = FlatHorizontalPath(5);
            var occupied = new HashSet<Vector3Int> { new(2, 0, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 4, occupied.Contains);

            // Y: 0,0,1,0,0 のオーバーパス（手前で登り、上を渡り、先で下る）
            // Y profile 0,0,1,0,0: ramp up, cross over, ramp down.
            Assert.AreEqual(0, infos[0].Position.y);
            Assert.AreEqual(0, infos[1].Position.y);
            Assert.AreEqual(1, infos[2].Position.y);
            Assert.AreEqual(0, infos[3].Position.y);
            Assert.AreEqual(0, infos[4].Position.y);

            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[0].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Up, infos[1].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[2].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Down, infos[3].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[4].VerticalDirection);
        }

        [Test]
        public void TallObstacleWithNoRampRoom_MarksEndpointsUnplaceable()
        {
            // 高さ2の障害物を3セルで跨ぐにはランプ長が足りない（高さ1・3セルは跨げてしまう点に注意）
            // A height-2 obstacle has no room to ramp within 3 cells (note: a height-1 obstacle in 3 cells IS crossable).
            var infos = FlatHorizontalPath(3);
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 2, occupied.Contains);

            // 端点を固定高さに戻しきれず両端が設置不可になる
            // The endpoints cannot return to the fixed height, so both ends become unplaceable.
            Assert.IsFalse(infos[0].Placeable);
            Assert.IsFalse(infos[2].Placeable);
        }
    }
}
