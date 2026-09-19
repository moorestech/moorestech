using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     設置高さの実値とHUD表示が同じ1本の値から出ることを検証（二重保持で食い違っていた経路ごと）
    ///     Verifies the actual placement height and the HUD both come from one value, per path that used to diverge
    /// </summary>
    public class PlacementHeightOffsetSingleSourceTest
    {
        [Test]
        public void ドラッグ中の高さ変更は解放で開始値へ戻り表示も同じ値になる()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            var published = SubscribeValues(heightOffset);

            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            dragState.AdjustHeightOffset(2);
            dragState.EndDrag();

            Assert.AreEqual(0, dragState.HeightOffset);
            Assert.AreEqual(0, published[^1], "the HUD kept the in-drag height after the drag restored it");
        }

        [Test]
        public void 持ち替えで高さが地表基準へ戻ると表示も戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(3);

            dragState.SyncSelectedBlock(new BlockId(2));

            Assert.AreEqual(0, heightOffset.Value, "the HUD kept the old height after a hotbar swap reset the actual one");
        }

        [Test]
        public void 建築モード離脱で高さを畳み再入場は地表基準から始まる()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(2);

            dragState.ClearDragAndHeight();

            // 同じブロックで入り直しても選択変化とみなされないため、離脱時に畳んでおく必要がある
            // Re-entering with the same block is not a selection change, so the height must fold on exit
            dragState.SyncSelectedBlock(new BlockId(1));
            Assert.AreEqual(0, dragState.HeightOffset);
        }

        [Test]
        public void 通常設置とベルトの高さは同じ1本を共有する()
        {
            var heightOffset = new PlacementHeightOffset();
            var commonDragState = new CommonBlockPlaceDragState(heightOffset);
            var beltDragState = new CommonBlockPlaceDragState(heightOffset);

            beltDragState.AdjustHeightOffset(1);

            Assert.AreEqual(1, commonDragState.HeightOffset);
            Assert.AreEqual(1, heightOffset.Value);
        }

        private static List<int> SubscribeValues(PlacementHeightOffset heightOffset)
        {
            var values = new List<int>();
            heightOffset.OnChanged.Subscribe(values.Add);
            return values;
        }
    }
}
