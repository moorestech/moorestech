using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     通常設置が地形を設置不可の理由にしないこと（ADR 0047）を入口ごと固定する
    ///     Pins that normal placement never blocks on terrain (ADR 0047), at its actual entry point
    /// </summary>
    public class NormalPlacementPreviewStepTest
    {
        // 地形へ食い込んでいても全セルが設置可のまま、地形干渉行も出ない
        // Every cell stays placeable and no terrain line is pushed, even while digging into the terrain
        [Test]
        public void 地形に食い込んでいても設置可のままで地形干渉行を出さない()
        {
            var placeInfos = BuildCells(3);
            var causes = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.None, PlacementBlockCause.None };
            var previewController = new AlwaysGroundOverlappingPreviewController();
            var feedback = new PlacementFeedback();

            var cursorIndex = NormalPlacementPreviewStep.Apply(previewController, placeInfos, causes, new Vector3Int(1, 0, 0), null, feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        // 地形接触を一度も問い合わせない。問い合わせたらADR 0047が撤回されている
        // The terrain contact is never queried; querying it would mean ADR 0047 was revoked
        [Test]
        public void 地形接触を問い合わせない()
        {
            var placeInfos = BuildCells(1);
            var causes = new List<PlacementBlockCause> { PlacementBlockCause.None };
            var previewController = new AlwaysGroundOverlappingPreviewController();

            NormalPlacementPreviewStep.Apply(previewController, placeInfos, causes, new Vector3Int(0, 0, 0), null, new PlacementFeedback());

            Assert.AreEqual(1, previewController.SetPreviewCallCount);
            Assert.AreEqual(0, previewController.DetectGroundOverlapsCallCount);
        }

        // 地形以外の共有原因は従来どおり積まれる
        // Non-terrain shared causes are reported as before
        [Test]
        public void 既存ブロックの理由は積む()
        {
            var placeInfos = BuildCells(3);
            var causes = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.ExistingBlock, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            NormalPlacementPreviewStep.Apply(new AlwaysGroundOverlappingPreviewController(), placeInfos, causes, new Vector3Int(1, 0, 0), null, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        private static List<PlaceInfo> BuildCells(int cellCount)
        {
            var placeInfos = new List<PlaceInfo>(cellCount);
            for (var i = 0; i < cellCount; i++) placeInfos.Add(new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true });
            return placeInfos;
        }

        // 全セルが地形へ接触していると答えるフェイク。呼ばれた回数も数える
        // A fake answering that every cell touches the terrain, counting how often it is called
        private class AlwaysGroundOverlappingPreviewController : IPlacementPreviewBlockGameObjectController
        {
            public int SetPreviewCallCount { get; private set; }
            public int DetectGroundOverlapsCallCount { get; private set; }

            private int _previewCellCount;

            public bool IsActive => true;

            public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster)
            {
                SetPreviewCallCount++;
                _previewCellCount = currentPlaceInfos.Count;
            }

            public IReadOnlyList<bool> DetectGroundOverlaps()
            {
                DetectGroundOverlapsCallCount++;

                var groundOverlaps = new List<bool>(_previewCellCount);
                for (var i = 0; i < _previewCellCount; i++) groundOverlaps.Add(true);
                return groundOverlaps;
            }

            public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }

            public void SetActive(bool active) { }

            public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
            {
                previewBlock = null;
                return false;
            }
        }
    }
}
