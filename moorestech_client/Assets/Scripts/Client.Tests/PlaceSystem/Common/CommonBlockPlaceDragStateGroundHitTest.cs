using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    // ドラッグ中の面種別が押下時の値で固定されることの検証
    // Verify that the surface kind stays frozen at the press for the whole drag
    public class CommonBlockPlaceDragStateGroundHitTest
    {
        // ブロック面から始めたドラッグは地面へ入っても追従しない
        // A drag started on a block face never follows the terrain, even over ground
        [Test]
        public void ブロック面から始めたドラッグは地面へ入っても面のまま()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.BlockFace);

            Assert.AreEqual(PlacementHitSurfaceKind.BlockFace, dragState.ResolveSurfaceKind(PlacementHitSurfaceKind.Ground));
        }

        // 地面から始めたドラッグは面をまたいでも追従したまま
        // A drag started on the ground keeps following even when the cursor crosses a block face
        [Test]
        public void 地面から始めたドラッグは面をまたいでも地面のまま()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);

            Assert.AreEqual(PlacementHitSurfaceKind.Ground, dragState.ResolveSurfaceKind(PlacementHitSurfaceKind.BlockFace));
        }

        // 押下していない間は当フレームの判定をそのまま返す
        // Outside a drag the current frame's judgement passes through
        [Test]
        public void 押下していない間は当フレームの判定を返す()
        {
            var dragState = new CommonBlockPlaceDragState();

            Assert.AreEqual(PlacementHitSurfaceKind.Ground, dragState.ResolveSurfaceKind(PlacementHitSurfaceKind.Ground));
            Assert.AreEqual(PlacementHitSurfaceKind.BlockFace, dragState.ResolveSurfaceKind(PlacementHitSurfaceKind.BlockFace));
        }

        // 解放すると次の押下まで固定が解ける
        // Releasing clears the freeze until the next press
        [Test]
        public void 解放すると固定が解ける()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.BlockFace);
            dragState.EndDrag();

            Assert.AreEqual(PlacementHitSurfaceKind.Ground, dragState.ResolveSurfaceKind(PlacementHitSurfaceKind.Ground));
        }
    }
}
