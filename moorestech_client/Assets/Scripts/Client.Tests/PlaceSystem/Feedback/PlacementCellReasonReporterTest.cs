using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     カーソル下セルのローカル理由が、地面接触リストと同じ添字で積まれることを検証
    ///     Verify the cursor cell's local reasons are pushed using the same index as the ground overlap list
    /// </summary>
    public class PlacementCellReasonReporterTest
    {
        [Test]
        public void 地形干渉と既存ブロック重複はこの順で積まれる()
        {
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.Report(0, true, new[] { true }, feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[1].TextKey);
        }

        [Test]
        public void カーソルセルが無ければ何も積まない()
        {
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.Report(-1, true, new List<bool>(), feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void ドラッグ列で解決した添字が地面接触リストの同じセルを指す()
        {
            // 通常設置と同じ経路: 同一のPlaceInfo列からcursorIndexを解決し、同じ長さの地面接触リストを引く
            // Same path as normal placement: resolve cursorIndex from the PlaceInfo list that produced the equal-length ground overlap list
            var placeInfos = BuildDragCells(3);
            var groundOverlaps = new List<bool> { false, true, false };
            Assert.AreEqual(placeInfos.Count, groundOverlaps.Count);

            var terrainFeedback = new PlacementFeedback();
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(1, 0, 0)), false, groundOverlaps, terrainFeedback);
            Assert.AreEqual(1, terrainFeedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, terrainFeedback.Lines[0].TextKey);

            var clearFeedback = new PlacementFeedback();
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(0, 0, 0)), false, groundOverlaps, clearFeedback);
            Assert.IsEmpty(clearFeedback.Lines);
        }

        [Test]
        public void カーソル一致セルが無い末尾フォールバックでも同じ添字を引く()
        {
            var placeInfos = BuildDragCells(3);
            var groundOverlaps = new List<bool> { false, false, true };
            var feedback = new PlacementFeedback();

            // カーソル位置が列に含まれないときは末尾セル。地面接触リストも末尾を見る
            // When the cursor cell is not in the list the resolver falls back to the last cell, and the ground overlap list is read at the same index
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(9, 9, 9)), false, groundOverlaps, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].TextKey);
        }

        private static List<PlaceInfo> BuildDragCells(int cellCount)
        {
            var placeInfos = new List<PlaceInfo>(cellCount);
            for (var i = 0; i < cellCount; i++) placeInfos.Add(new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true });
            return placeInfos;
        }
    }
}
