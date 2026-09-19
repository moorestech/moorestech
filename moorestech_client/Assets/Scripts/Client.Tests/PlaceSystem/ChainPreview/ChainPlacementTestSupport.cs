using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結レイアウトテストで共有するサーバー起動とテストダブル
    ///     Shared server bootstrap and test doubles for chain-layout tests
    /// </summary>
    public static class ChainPlacementTestSupport
    {
        public static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static ChainPlacePreviewState CreateChainState(Guid tutorialGuid, BlockMasterElement anchorMaster, BlockId chainBlockId, Vector3Int chainOffset, BlockDirection chainLocalDirection)
        {
            var state = new ChainPlacePreviewState();
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(anchorMaster.BlockGuid);
            state.SetChain(tutorialGuid, anchorBlockId, new List<ChainGhost> { new(chainBlockId, chainOffset, chainLocalDirection) });
            return state;
        }

        // 指定セルだけ塞がっている既存ブロック問い合わせのテストダブル。nullなら何も塞がない
        // Existing-block query double occupying only the given cell; null occupies nothing
        public class StubExistingBlockQuery : IExistingBlockQuery
        {
            private readonly Vector3Int? _occupiedCell;
            public StubExistingBlockQuery(Vector3Int? occupiedCell) => _occupiedCell = occupiedCell;
            public bool IsOverlapping(PlaceInfo placeInfo) => _occupiedCell.HasValue && placeInfo.Position == _occupiedCell.Value;
        }

        public class AlwaysAlignedGroundQuery : IChainGroundQuery
        {
            public ChainCellBlockReason ResolveGroundAlignment(Vector3Int cell, BlockDirection direction, Vector3Int blockSize, int heightOffset) => ChainCellBlockReason.None;
        }
    }
}
