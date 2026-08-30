using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     連結レイアウトの設置不可検査を検証
    ///     Verifies that placements whose chain layout cannot fit are rejected
    /// </summary>
    public class ChainPlacementReporterTest
    {
        private static readonly Guid ChainTutorialGuid = Guid.Parse("33333333-0000-0000-0000-000000000001");
        private static readonly Vector3Int CursorCell = new(10, 0, 10);
        private static readonly Vector3Int ChainOffset = new(-1, 0, 2);

        [Test]
        public void 連結セルが既存ブロックで塞がれているとカーソルセルが設置不可になる()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            // North設置なので連結セルは無回転の CursorCell + ChainOffset に載る
            // Placing North keeps the chain cell at the unrotated CursorCell + ChainOffset
            var occupied = new StubExistingBlockQuery(CursorCell + ChainOffset);
            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateChainState(chestMaster), occupied, new AlwaysAlignedGroundQuery(), feedback);

            Assert.IsFalse(placeInfos[0].Placeable, "a blocked chain cell left the anchor placeable");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceChainBlocked) }, feedback.Lines);
        }

        [Test]
        public void 連結セルが空いていれば設置可のまま()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateChainState(chestMaster), new StubExistingBlockQuery(null), new AlwaysAlignedGroundQuery(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "an open chain cell blocked the anchor");
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 設置向きを東へ回すと連結セルも回る()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var state = CreateChainState(chestMaster);

            // North基準の(-1,0,2)はEast設置では回転して別セルへ移る。Northで塞いだセルはEastでは効かない
            // The North-basis (-1,0,2) rotates away under an East placement, so a cell blocking North must not block East
            var eastInfos = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.East) };
            var occupiedNorthCell = new StubExistingBlockQuery(CursorCell + ChainOffset);
            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(eastInfos, chestMaster, 0, state, occupiedNorthCell, new AlwaysAlignedGroundQuery(), new PlacementFeedback());
            Assert.IsTrue(eastInfos[0].Placeable, "the chain cell did not rotate with the placement direction");

            // 回転後の実セルを塞ぐと不可になる。実セルは本番と同じ換算（ConvertBlockLocalToWorldCell）で得る
            // Blocking the actually rotated cell rejects it; the cell comes from the same production conversion
            var footprint = new BlockPositionInfo(CursorCell, BlockDirection.East, chestMaster.BlockSize);
            var rotatedCell = footprint.ConvertBlockLocalToWorldCell(ChainOffset);
            var eastInfos2 = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.East) };
            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(eastInfos2, chestMaster, 0, state, new StubExistingBlockQuery(rotatedCell), new AlwaysAlignedGroundQuery(), new PlacementFeedback());
            Assert.IsFalse(eastInfos2[0].Placeable, "the rotated chain cell was not checked");
        }

        [Test]
        public void 地形が揃わない連結セルは設置不可になる()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.North) };

            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateChainState(chestMaster), new StubExistingBlockQuery(null), new NeverAlignedGroundQuery(), new PlacementFeedback());

            Assert.IsFalse(placeInfos[0].Placeable, "misaligned ground left the anchor placeable");
        }

        [Test]
        public void 連結対象でないブロックは素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var generatorMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(CursorCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            // チェスト用の連結定義しか無いので発電機は素通り
            // Only the chest anchors a chain, so the generator passes untouched
            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, generatorMaster, 0, CreateChainState(chestMaster), new StubExistingBlockQuery(CursorCell + ChainOffset), new NeverAlignedGroundQuery(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        private static ChainPlacePreviewState CreateChainState(Mooresmaster.Model.BlocksModule.BlockMasterElement anchorMaster)
        {
            var state = new ChainPlacePreviewState();
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(anchorMaster.BlockGuid);
            var chain = new List<ChainGhost> { new(ForUnitTestModBlockId.ChestId, ChainOffset, BlockDirection.North) };
            state.SetChain(ChainTutorialGuid, anchorBlockId, chain);
            return state;
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int position, BlockDirection direction)
        {
            return new PlaceInfo
            {
                Position = position,
                Direction = direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                Placeable = true,
            };
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 指定セルだけ塞がっている既存ブロック問い合わせのテストダブル
        // Existing-block query double occupying only the given cell
        private class StubExistingBlockQuery : IExistingBlockQuery
        {
            private readonly Vector3Int? _occupiedCell;
            public StubExistingBlockQuery(Vector3Int? occupiedCell) => _occupiedCell = occupiedCell;
            public bool IsOverlapping(PlaceInfo placeInfo) => _occupiedCell.HasValue && placeInfo.Position == _occupiedCell.Value;
        }

        private class AlwaysAlignedGroundQuery : IChainGroundQuery
        {
            public bool IsGroundAligned(Vector3Int cell, BlockDirection direction, Vector3Int blockSize) => true;
        }

        private class NeverAlignedGroundQuery : IChainGroundQuery
        {
            public bool IsGroundAligned(Vector3Int cell, BlockDirection direction, Vector3Int blockSize) => false;
        }
    }
}
