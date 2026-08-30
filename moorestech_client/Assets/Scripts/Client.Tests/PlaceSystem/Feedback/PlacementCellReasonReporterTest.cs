using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     カーソル下セル理由が同じ添字で積まれることを検証
    ///     Verify the cursor cell's reasons use the same index
    /// </summary>
    public class PlacementCellReasonReporterTest
    {
        [Test]
        public void 地形干渉と既存ブロック重複はこの順で積まれる()
        {
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.Report(0, PlacementBlockCause.ExistingBlock, new[] { true }, feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[1].Key.Key);
        }

        [Test]
        public void カーソルセルが無ければ何も積まない()
        {
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.Report(-1, PlacementBlockCause.ExistingBlock, new List<bool>(), feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void ドラッグ列で解決した添字が地面接触リストの同じセルを指す()
        {
            // 通常設置と同じ経路で解決・地面接触リストを引く
            // Same path as normal placement: resolve and read the ground overlap list
            var placeInfos = BuildDragCells(3);
            var groundOverlaps = new List<bool> { false, true, false };
            Assert.AreEqual(placeInfos.Count, groundOverlaps.Count);

            var terrainFeedback = new PlacementFeedback();
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(1, 0, 0)), PlacementBlockCause.None, groundOverlaps, terrainFeedback);
            Assert.AreEqual(1, terrainFeedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, terrainFeedback.Lines[0].Key.Key);

            var clearFeedback = new PlacementFeedback();
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(0, 0, 0)), PlacementBlockCause.None, groundOverlaps, clearFeedback);
            Assert.IsEmpty(clearFeedback.Lines);
        }

        [Test]
        public void カーソル一致セルが無い末尾フォールバックでも同じ添字を引く()
        {
            var placeInfos = BuildDragCells(3);
            var groundOverlaps = new List<bool> { false, false, true };
            var feedback = new PlacementFeedback();

            // 非含有時は末尾セル。地面接触も末尾を見る
            // When not in the list, falls back to the last cell; ground overlap is read there too
            PlacementCellReasonReporter.Report(PlacementCursorCellResolver.Resolve(placeInfos, new Vector3Int(9, 9, 9)), PlacementBlockCause.None, groundOverlaps, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 原因が無ければ地形干渉行だけを積む()
        {
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.Report(0, PlacementBlockCause.None, new[] { true }, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void カーソルセルと同じ添字の原因がそのまま行になる()
        {
            var placeInfos = BuildDragCells(3);
            placeInfos[1].Placeable = false;
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.ExistingBlock, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(1, 0, 0), new List<bool> { false, false, false }, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void カーソルセル以外の原因は行にならない()
        {
            var placeInfos = BuildDragCells(3);
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.ExistingBlock, PlacementBlockCause.None, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(1, 0, 0), new List<bool> { false, false, false }, feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        // 地形を見ない入口は地形の理由を積まず、どのセルも落とさない
        // The terrain-blind entry pushes no terrain line and drops no cell
        [Test]
        public void 地形を見ない入口は地形の理由を積まない()
        {
            var placeInfos = BuildDragCells(3);
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.None, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            var cursorIndex = PlacementCellReasonReporter.ResolveCursorAndReportCauses(placeInfos, cellCauses, new Vector3Int(1, 0, 0), feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        // 地形以外の共有原因は地形を見ない入口でも積まれる
        // Non-terrain shared causes are still reported by the terrain-blind entry
        [Test]
        public void 地形を見ない入口でも既存ブロックの理由は積む()
        {
            var placeInfos = BuildDragCells(3);
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.ExistingBlock, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.ResolveCursorAndReportCauses(placeInfos, cellCauses, new Vector3Int(1, 0, 0), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        private static List<PlaceInfo> BuildDragCells(int cellCount)
        {
            var placeInfos = new List<PlaceInfo>(cellCount);
            for (var i = 0; i < cellCount; i++) placeInfos.Add(new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true });
            return placeInfos;
        }
    }
}
