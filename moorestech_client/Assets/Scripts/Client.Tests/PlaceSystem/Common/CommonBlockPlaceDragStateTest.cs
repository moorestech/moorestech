using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
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
    }
}
