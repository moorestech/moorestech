using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorSlopePathBuilderTest
    {
        // 単セルはキー回転の向きをそのまま使う
        // A single cell keeps the key-rotated direction as-is
        [Test]
        public void 単セルは選択した坂と回転方向で1個だけ返る()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(3, 5, 7), new Vector3Int(3, 5, 7), true, BlockDirection.West, BlockVerticalDirection.Up);

            Assert.AreEqual(1, placeInfos.Count);
            Assert.AreEqual(new Vector3Int(3, 5, 7), placeInfos[0].Position);
            Assert.AreEqual(BlockDirection.West, placeInfos[0].Direction);
            Assert.AreEqual(BlockVerticalDirection.Up, placeInfos[0].VerticalDirection);
            Assert.IsTrue(placeInfos[0].Placeable);
        }

        // 上りは終点高さを無視し毎セル+1で伸びる
        // Up ignores the end height and grows +1 per cell
        [Test]
        public void 上りは終点の高さを無視して毎セル1段上がる()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(3, -10, 0), false, BlockDirection.North, BlockVerticalDirection.Up);

            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(2, 2, 0), new Vector3Int(3, 3, 0) },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));
            Assert.IsTrue(placeInfos.All(info => info.Direction == BlockDirection.East));
        }

        // 下りは毎セル-1で潜る
        // Down descends one per cell
        [Test]
        public void 下りは毎セル1段下がる()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 2), true, BlockDirection.North, BlockVerticalDirection.Down);

            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, -1, 1), new Vector3Int(0, -2, 2) },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Down));
        }

        // L字でも角のセルが坂のまま一定勾配で続く
        // An L-shaped run keeps the corner cell sloped at the same constant grade
        [Test]
        public void L字の角も坂のまま一定勾配で続く()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 2), true, BlockDirection.North, BlockVerticalDirection.Up);

            // Z2マス→X2マスに曲がる経路（角はindex2）
            // Path: 2 cells in Z then a 2-cell turn in X (corner at index 2)
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 1), new Vector3Int(0, 2, 2),
                    new Vector3Int(1, 3, 2), new Vector3Int(2, 4, 2),
                },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));

            // 角は次セルへ向き、末尾は前セルの向きを継ぐ
            // Corner faces the next cell; the tail keeps the previous cell's facing
            Assert.AreEqual(BlockDirection.North, placeInfos[1].Direction);
            Assert.AreEqual(BlockDirection.East, placeInfos[2].Direction);
            Assert.AreEqual(BlockDirection.East, placeInfos[4].Direction);
        }
    }
}
