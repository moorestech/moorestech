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

namespace Client.Tests.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     上下向きの設置で連結レイアウトが例外を投げず、不可理由付きで弾かれることを検証
    ///     Verifies that an up/down placement rejects the chain layout with a reason instead of throwing
    /// </summary>
    public class ChainLayoutVerticalAnchorTest
    {
        private static readonly Guid ChainTutorialGuid = Guid.Parse("33333333-0000-0000-0000-000000000002");
        private static readonly Vector3Int CursorCell = new(10, 0, 10);
        private static readonly Vector3Int ChainOffset = new(-1, 0, 2);

        // 風車チャレンジの1本目と同じく East ローカルのゴーストは上下アンカーで12方位へ回せない
        // Like the windmill challenge's first ghost, an East-local ghost cannot rotate into the 12 directions under an up/down anchor
        [TestCase(BlockDirection.UpEast)]
        [TestCase(BlockDirection.UpNorth)]
        [TestCase(BlockDirection.DownWest)]
        public void 上下向きの設置は例外を投げず理由付きで設置不可になる(BlockDirection placeDirection)
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { new() { Position = CursorCell, Direction = placeDirection, Placeable = true } };
            var feedback = new PlacementFeedback();

            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateEastChainState(chestMaster), new EmptyExistingBlockQuery(), new AlwaysAlignedGroundQuery(), true, 0, feedback);

            Assert.IsFalse(placeInfos[0].Placeable, "an up/down anchor stayed placeable with a chain layout");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceChainVerticalDirection) }, feedback.Lines);
        }

        [Test]
        public void 上下向きではゴーストを解決せず空で返す()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var chain = new List<ChainGhost> { new(ForUnitTestModBlockId.ChestId, ChainOffset, BlockDirection.East) };
            var results = new List<ChainLayoutResolver.ResolvedChainGhost>();

            var reason = ChainLayoutResolver.Resolve(CursorCell, BlockDirection.UpEast, chestMaster.BlockSize, chain, new EmptyExistingBlockQuery(), new AlwaysAlignedGroundQuery(), true, 0, results);

            Assert.AreEqual(ChainCellBlockReason.VerticalAnchor, reason);
            CollectionAssert.IsEmpty(results, "ghosts were resolved for an up/down anchor");
        }

        [Test]
        public void 水平向きの設置は従来どおりゴーストを解決する()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var chain = new List<ChainGhost> { new(ForUnitTestModBlockId.ChestId, ChainOffset, BlockDirection.East) };
            var results = new List<ChainLayoutResolver.ResolvedChainGhost>();

            var reason = ChainLayoutResolver.Resolve(CursorCell, BlockDirection.South, chestMaster.BlockSize, chain, new EmptyExistingBlockQuery(), new AlwaysAlignedGroundQuery(), true, 0, results);

            Assert.AreEqual(ChainCellBlockReason.None, reason);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BlockDirection.West, results[0].WorldDirection, "East rotated by a South anchor should face West");
        }

        private static ChainPlacePreviewState CreateEastChainState(Mooresmaster.Model.BlocksModule.BlockMasterElement anchorMaster)
        {
            var state = new ChainPlacePreviewState();
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(anchorMaster.BlockGuid);
            state.SetChain(ChainTutorialGuid, anchorBlockId, new List<ChainGhost> { new(ForUnitTestModBlockId.ChestId, ChainOffset, BlockDirection.East) });
            return state;
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private class EmptyExistingBlockQuery : IExistingBlockQuery
        {
            public bool IsOverlapping(PlaceInfo placeInfo) => false;
        }

        private class AlwaysAlignedGroundQuery : IChainGroundQuery
        {
            public ChainCellBlockReason ResolveGroundAlignment(Vector3Int cell, BlockDirection direction, Vector3Int blockSize, int heightOffset) => ChainCellBlockReason.None;
        }
    }
}
