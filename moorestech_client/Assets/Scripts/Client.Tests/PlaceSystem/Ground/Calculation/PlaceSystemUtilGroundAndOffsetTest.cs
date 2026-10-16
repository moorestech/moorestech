using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground.Calculation
{
    public class PlaceSystemUtilGroundAndOffsetTest
    {
        [Test]
        public void surfaceType_null_地面ヒット時_中央寄せスナップ()
        {
            // 単位サイズ・高さ補正なしのスナップ
            // Snap a unit-sized block without height offset
            var hitPoint = new Vector3(5.3f, 4.0f, 5.7f);

            var pos = PlaceSystemUtil.CalcPlacePointBySize(Vector3Int.one, hitPoint, 0, (PreviewSurfaceType?)null, 0f);

            Assert.AreEqual(new Vector3Int(5, 4, 5), pos);
        }

        // Q/Eの高さ補正は面ヒットにも有効
        // Q/E height offset also applies to face hits
        [Test]
        public void XZ_Y_上面_heightOffset_2_でY方向に持ち上がること()
        {
            var hitPoint = new Vector3(5.5f, 6.0f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePointBySize(Vector3Int.one, hitPoint, 2, PreviewSurfaceType.XZ_Y, 0f);

            Assert.AreEqual(new Vector3Int(5, 8, 5), pos);
        }

        [Test]
        public void YX_Z_南面_heightOffset_negative1_でY方向に下がること()
        {
            var hitPoint = new Vector3(5.5f, 5.5f, 6.0f);

            var pos = PlaceSystemUtil.CalcPlacePointBySize(Vector3Int.one, hitPoint, -1, PreviewSurfaceType.YX_Z, 0f);

            Assert.AreEqual(new Vector3Int(5, 4, 6), pos);
        }

        [Test]
        public void YZ_X_東面_heightOffset_3_でY方向に持ち上がること()
        {
            var hitPoint = new Vector3(6.0f, 5.5f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePointBySize(Vector3Int.one, hitPoint, 3, PreviewSurfaceType.YZ_X, 0f);

            Assert.AreEqual(new Vector3Int(6, 8, 5), pos);
        }
    }
}
