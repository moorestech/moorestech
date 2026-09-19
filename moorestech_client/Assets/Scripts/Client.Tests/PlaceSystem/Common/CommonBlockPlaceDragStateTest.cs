using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     押下が登録された解放だけがドラッグ終了として成立することを検証（設置送信の可否はこの戻り値で決まる）
    ///     Verify only a release with a registered press ends a drag (this return value gates the placement send)
    /// </summary>
    public class CommonBlockPlaceDragStateTest
    {
        [Test]
        public void 押下未登録の解放は設置送信へ進まず高さも書き換えない()
        {
            var dragState = new CommonBlockPlaceDragState();

            Assert.IsFalse(dragState.EndDrag());
            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 押下済みの解放はドラッグ終了として成立し高さを開始値へ戻す()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(new Vector3Int(1, 2, 3), PlacementHitSurfaceKind.Ground);

            Assert.IsTrue(dragState.EndDrag());
            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 同じ解放を二度受けても二度目は成立しない()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(new Vector3Int(1, 2, 3), PlacementHitSurfaceKind.Ground);

            Assert.IsTrue(dragState.EndDrag());
            Assert.IsFalse(dragState.EndDrag());
        }

        [Test]
        public void 押下から解放までが進行中のドラッグとして数えられる()
        {
            var dragState = new CommonBlockPlaceDragState();

            Assert.IsFalse(dragState.IsDragging);

            dragState.BeginDrag(new Vector3Int(1, 2, 3), PlacementHitSurfaceKind.Ground);
            Assert.IsTrue(dragState.IsDragging);

            dragState.EndDrag();
            Assert.IsFalse(dragState.IsDragging);
        }

        [Test]
        public void 押下位置は開始点として返り解放後は現在位置へ戻る()
        {
            var dragState = new CommonBlockPlaceDragState();
            var cursorCell = new Vector3Int(5, 0, 5);

            Assert.AreEqual(cursorCell, dragState.ResolveDragStartCell(cursorCell));

            dragState.BeginDrag(new Vector3Int(1, 0, 1), PlacementHitSurfaceKind.Ground);
            Assert.AreEqual(new Vector3Int(1, 0, 1), dragState.ResolveDragStartCell(cursorCell));

            dragState.EndDrag();
            Assert.AreEqual(cursorCell, dragState.ResolveDragStartCell(cursorCell));
        }

        [Test]
        public void 別ブロックへ切替えると高さオフセットが0へ戻る()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(5);

            dragState.SyncSelectedBlock(new BlockId(2));

            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 同じブロックの再選択では高さオフセットが保たれる()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(5);

            dragState.SyncSelectedBlock(new BlockId(1));

            Assert.AreEqual(5, dragState.HeightOffset);
        }

        [Test]
        public void ClearDragを挟んでも同一ブロックなら高さオフセットは保たれる()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(5);

            // 配置システムを跨いだDisable相当の解除。高さの基準はブロック切替だけが動かす
            // Simulates the Disable-equivalent teardown across place systems; only a block switch moves the height baseline
            dragState.ClearDrag();
            dragState.SyncSelectedBlock(new BlockId(1));

            Assert.AreEqual(5, dragState.HeightOffset);
        }

        [Test]
        public void ドラッグ中に上げた高さは解放で開始値へ戻る()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(2);
            dragState.BeginDrag(new Vector3Int(0, 0, 0), PlacementHitSurfaceKind.Ground);
            dragState.AdjustHeightOffset(3);

            Assert.IsTrue(dragState.EndDrag());
            Assert.AreEqual(2, dragState.HeightOffset);
        }

        [Test]
        public void 解放フレームはドラッグを畳み次の列は現在位置から始まる()
        {
            var dragState = new CommonBlockPlaceDragState();
            var cursorCell = new Vector3Int(8, 2, 3);
            dragState.BeginDrag(new Vector3Int(1, 2, 3), PlacementHitSurfaceKind.Ground);
            dragState.AdjustHeightOffset(2);

            // 空や距離外で離したフレームでも畳む。残ると押していないのに古い開始点から列が伸びる
            // Fold even on a sky or out-of-reach release; a leftover session extends a run from the stale start with no button held
            Assert.IsTrue(dragState.EndDragOnRelease(true), "a release with a registered press must allow the send");

            Assert.IsFalse(dragState.IsDragging, "the drag survived its release frame");
            Assert.AreEqual(cursorCell, dragState.ResolveDragStartCell(cursorCell));
            Assert.AreEqual(0, dragState.HeightOffset, "the in-drag height was not restored on release");
        }

        [Test]
        public void 押している間は開始点と高さを保ち送信へ進まない()
        {
            var dragState = new CommonBlockPlaceDragState();
            var startCell = new Vector3Int(1, 2, 3);
            dragState.BeginDrag(startCell, PlacementHitSurfaceKind.Ground);
            dragState.AdjustHeightOffset(2);

            Assert.IsFalse(dragState.EndDragOnRelease(false));

            Assert.IsTrue(dragState.IsDragging);
            Assert.AreEqual(startCell, dragState.ResolveDragStartCell(new Vector3Int(8, 2, 3)));
            Assert.AreEqual(2, dragState.HeightOffset);
        }

        [Test]
        public void 押下未登録の解放は送信へ進まない()
        {
            var dragState = new CommonBlockPlaceDragState();

            Assert.IsFalse(dragState.EndDragOnRelease(true));
        }
    }
}
