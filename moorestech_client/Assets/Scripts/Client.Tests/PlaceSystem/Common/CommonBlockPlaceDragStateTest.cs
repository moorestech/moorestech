using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
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

            // Enableのセンチネル-1が高さへ漏れると以後の設置が1段沈む
            // Leaking Enable's -1 sentinel into the height would sink later placements by one step
            dragState.SetClickStartHeightOffset(-1);

            Assert.IsFalse(dragState.EndDrag());
            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 押下済みの解放はドラッグ終了として成立し高さを開始値へ戻す()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.SetClickStartHeightOffset(-1);
            dragState.BeginDrag(new Vector3Int(1, 2, 3));

            Assert.IsTrue(dragState.EndDrag());
            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 同じ解放を二度受けても二度目は成立しない()
        {
            var dragState = new CommonBlockPlaceDragState();
            dragState.BeginDrag(new Vector3Int(1, 2, 3));

            Assert.IsTrue(dragState.EndDrag());
            Assert.IsFalse(dragState.EndDrag());
        }

        [Test]
        public void 押下位置は開始点として返り解放後は現在位置へ戻る()
        {
            var dragState = new CommonBlockPlaceDragState();
            var placePoint = new Vector3Int(5, 0, 5);

            Assert.AreEqual(placePoint, dragState.ResolveDragStartPoint(placePoint));

            dragState.BeginDrag(new Vector3Int(1, 0, 1));
            Assert.AreEqual(new Vector3Int(1, 0, 1), dragState.ResolveDragStartPoint(placePoint));

            dragState.EndDrag();
            Assert.AreEqual(placePoint, dragState.ResolveDragStartPoint(placePoint));
        }
    }
}
