using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.Block.Interface;
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
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(5);

            heightOffset.SyncSelectedTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "second", new global::Game.Blueprint.BlueprintJsonObject()));

            Assert.AreEqual(0, heightOffset.Value);
        }

        [Test]
        public void 同じブロックの再選択では高さオフセットが保たれる()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(5);

            heightOffset.SyncSelectedTarget(firstTarget);

            Assert.AreEqual(5, heightOffset.Value);
        }

        [Test]
        public void スポイトで同じブロックの別向きを拾っても高さオフセットは保たれる()
        {
            var heightOffset = new PlacementHeightOffset();
            var blockGuid = Guid.NewGuid();
            heightOffset.SyncSelectedTarget(new BlockPlacementTarget(blockGuid, BlockDirection.North));
            heightOffset.Adjust(3);

            // 向き違いは持ち替えではない。旧来のBlockId比較と同じく高さを保つ
            // A different facing is not a switch; keep the height just like the former BlockId comparison
            heightOffset.SyncSelectedTarget(new BlockPlacementTarget(blockGuid, BlockDirection.East));

            Assert.AreEqual(3, heightOffset.Value);
        }

        [Test]
        public void 同じIdでも種別が違えば高さオフセットは0へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var sharedGuid = Guid.NewGuid();
            heightOffset.SyncSelectedTarget(new BlockPlacementTarget(sharedGuid, null));
            heightOffset.Adjust(3);

            heightOffset.SyncSelectedTarget(new BlueprintPlacementTarget(sharedGuid, "blueprint", new global::Game.Blueprint.BlueprintJsonObject()));

            Assert.AreEqual(0, heightOffset.Value);
        }

        [Test]
        public void ClearDragを挟んでも同一ブロックなら高さオフセットは保たれる()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
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
