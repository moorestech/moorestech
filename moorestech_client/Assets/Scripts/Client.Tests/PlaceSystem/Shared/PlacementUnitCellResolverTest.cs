using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class PlacementUnitCellResolverTest
    {
        [Test]
        public void 東面ヒットは外側セルでYは浮かない()
        {
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(3.07f, 32.55f, 2.25f), PreviewSurfaceType.YZ_X, 0f, 0);
            Assert.AreEqual(new Vector3Int(3, 32, 2), cell);
        }

        [Test]
        public void 天面ヒットは上のセル()
        {
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(2.63f, 33.07f, 2.09f), PreviewSurfaceType.XZ_Y, 0f, 0);
            Assert.AreEqual(new Vector3Int(2, 33, 2), cell);
        }

        [Test]
        public void 地面ヒットに高さオフセットが乗る()
        {
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(0.5f, 32f, 0.5f), null, 0f, 2);
            Assert.AreEqual(new Vector3Int(0, 34, 0), cell);
        }
    }
}
