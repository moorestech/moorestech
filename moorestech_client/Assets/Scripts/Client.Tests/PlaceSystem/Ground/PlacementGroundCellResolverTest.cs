using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地形最高点からセルYを決める純粋変換の検証
    // Verify the pure conversion from the terrain max height to a cell Y
    public class PlacementGroundCellResolverTest
    {
        // 端数のある地表は上のセルへ切り上げる
        // Fractional ground rounds up to the cell above
        [Test]
        public void 端数のある地表は上のセルへ切り上げる()
        {
            Assert.AreEqual(33, PlacementGroundCellResolver.ResolveCellY(32.4f, 0));
            Assert.AreEqual(33, PlacementGroundCellResolver.ResolveCellY(32.9f, 0));
            Assert.AreEqual(1, PlacementGroundCellResolver.ResolveCellY(0.1f, 0));
        }

        // 整数ちょうどの地表は浮かせない
        // Ground exactly on an integer must not float
        [Test]
        public void 整数ちょうどの地表は浮かせない()
        {
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(32f, 0));
            Assert.AreEqual(0, PlacementGroundCellResolver.ResolveCellY(0f, 0));
        }

        // 整数近傍の浮動小数点誤差で1段浮かない
        // Floating-point noise near an integer must not float one cell
        [Test]
        public void 整数近傍の誤差で一段浮かない()
        {
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(32.0001f, 0));
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(31.9999f, 0));
        }

        // 負の高さでも切り上げ規約は変わらない
        // The round-up convention is unchanged for negative heights
        [Test]
        public void 負の高さでも切り上げる()
        {
            Assert.AreEqual(-3, PlacementGroundCellResolver.ResolveCellY(-3.4f, 0));
            Assert.AreEqual(-4, PlacementGroundCellResolver.ResolveCellY(-4f, 0));
        }

        // 手動オフセットは地形解決後に加算する
        // The manual offset is added after the terrain resolution
        [Test]
        public void 手動オフセットは地形解決後に加算される()
        {
            Assert.AreEqual(35, PlacementGroundCellResolver.ResolveCellY(32.4f, 2));
            Assert.AreEqual(31, PlacementGroundCellResolver.ResolveCellY(32.4f, -2));
        }
    }
}
