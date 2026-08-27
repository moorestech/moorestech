using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地形追従を許す条件の検証
    // Verify when terrain following is allowed
    public class PlacementGroundFollowPolicyTest
    {
        // ブロック面ヒットのカーソルセルは追従しない
        // A block-face hit does not follow the terrain
        [Test]
        public void カーソルセルは地面ヒットのときだけ追従する()
        {
            Assert.IsTrue(PlacementGroundFollowPolicy.ShouldFollowCursorCell(true));
            Assert.IsFalse(PlacementGroundFollowPolicy.ShouldFollowCursorCell(false));
        }

        // XZへ伸びた列は追従する
        // A run extended along X or Z follows the terrain
        [Test]
        public void 横方向の列は追従する()
        {
            Assert.IsTrue(PlacementGroundFollowPolicy.ShouldFollowRunCells(true, PlacementRunAxis.X));
            Assert.IsTrue(PlacementGroundFollowPolicy.ShouldFollowRunCells(true, PlacementRunAxis.Z));
        }

        // Y軸列を追従させると全セルが1セルへ潰れる
        // Following a Y-axis run would collapse every cell into one
        [Test]
        public void 縦積み列は追従しない()
        {
            Assert.IsFalse(PlacementGroundFollowPolicy.ShouldFollowRunCells(true, PlacementRunAxis.Y));
        }

        // ブロック面ヒットならどの軸でも追従しない
        // A block-face hit never follows, on any axis
        [Test]
        public void ブロック面ヒットの列は追従しない()
        {
            Assert.IsFalse(PlacementGroundFollowPolicy.ShouldFollowRunCells(false, PlacementRunAxis.X));
            Assert.IsFalse(PlacementGroundFollowPolicy.ShouldFollowRunCells(false, PlacementRunAxis.Z));
            Assert.IsFalse(PlacementGroundFollowPolicy.ShouldFollowRunCells(false, PlacementRunAxis.Y));
        }
    }
}
