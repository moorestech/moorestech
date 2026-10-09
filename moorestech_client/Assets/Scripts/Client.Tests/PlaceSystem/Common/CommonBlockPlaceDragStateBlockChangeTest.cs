using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     持ち替えでドラッグを捨てるのはブロック種が変わったときだけであることを検証
    ///     Verify a drag is discarded on a switch only when the block kind changes
    /// </summary>
    public class CommonBlockPlaceDragStateBlockChangeTest
    {
        [Test]
        public void ドラッグ中に同じブロック種の別向きへ持ち替えてもドラッグが続く()
        {
            var dragState = new CommonBlockPlaceDragState(new PlacementHeightOffset());
            var blockGuid = Guid.NewGuid();
            dragState.DiscardForBlockChange(blockGuid);
            dragState.BeginDrag(new Vector3Int(1, 0, 1), PlacementHitSurfaceKind.Ground);

            dragState.DiscardForBlockChange(blockGuid);

            Assert.IsTrue(dragState.IsDragging, "a same-kind switch discarded the drag");
        }

        [Test]
        public void ドラッグ中に別のブロック種へ持ち替えるとドラッグを捨てる()
        {
            var dragState = new CommonBlockPlaceDragState(new PlacementHeightOffset());
            dragState.DiscardForBlockChange(Guid.NewGuid());
            dragState.BeginDrag(new Vector3Int(1, 0, 1), PlacementHitSurfaceKind.Ground);

            dragState.DiscardForBlockChange(Guid.NewGuid());

            Assert.IsFalse(dragState.IsDragging, "a different-kind switch kept the drag");
        }
    }
}
