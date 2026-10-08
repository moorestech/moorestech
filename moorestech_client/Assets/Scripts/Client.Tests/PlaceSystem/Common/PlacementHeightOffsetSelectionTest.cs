using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     持ち替えで高さを地表へ戻すか保つかの規則を検証
    ///     Verifies when switching targets returns the height to ground or keeps it
    /// </summary>
    public class PlacementHeightOffsetSelectionTest
    {
        [Test]
        public void 別ブロックへ切替えると高さオフセットが0へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first");
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(5);

            heightOffset.SyncSelectedTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "second"));

            Assert.AreEqual(0, heightOffset.Value);
        }

        [Test]
        public void 同じブロックの再選択では高さオフセットが保たれる()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first");
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(5);

            heightOffset.SyncSelectedTarget(firstTarget);

            Assert.AreEqual(5, heightOffset.Value);
        }

        [Test]
        public void ClearDragを挟んでも同一ブロックなら高さオフセットは保たれる()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first");
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(5);

            // 配置システムを跨いだDisable相当の解除。高さの基準はブロック切替だけが動かす
            // Simulates the Disable-equivalent teardown across place systems; only a block switch moves the height baseline
            new CommonBlockPlaceDragState(heightOffset).ClearDrag();
            heightOffset.SyncSelectedTarget(firstTarget);

            Assert.AreEqual(5, heightOffset.Value);
        }
    }
}
