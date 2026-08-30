using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.Tutorial;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結ゴースト群をカーソルへ追従表示、塞がりは不可色
    ///     Follows the placement cursor with the chain ghosts; a blocked cell's ghost turns the not-placeable color
    /// </summary>
    public class ChainPlacementPreviewPart
    {
        private readonly ChainPlacePreviewState _state;
        private readonly IExistingBlockQuery _existingBlockQuery;
        private readonly IChainGroundQuery _groundQuery;
        private readonly List<TutorialGhostEntry> _ghostEntries = new();
        private readonly List<ChainLayoutResolver.ResolvedChainGhost> _resolvedBuffer = new();
        
        public ChainPlacementPreviewPart(ChainPlacePreviewState state, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery)
        {
            _state = state;
            _existingBlockQuery = existingBlockQuery;
            _groundQuery = groundQuery;
        }
        
        public void Apply(PlaceInfo cursorPlaceInfo, BlockMasterElement holdingBlockMaster)
        {
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!_state.TryGetChain(holdingBlockId, out var chain) || chain.Count == 0)
            {
                Hide();
                return;
            }
            
            ChainLayoutResolver.Resolve(cursorPlaceInfo.Position, cursorPlaceInfo.Direction, holdingBlockMaster.BlockSize, chain, _resolvedBuffer);
            for (var i = 0; i < _resolvedBuffer.Count; i++)
            {
                var resolved = _resolvedBuffer[i];
                if (_ghostEntries.Count <= i) _ghostEntries.Add(new TutorialGhostEntry($"chain-preview-{i}"));
                
                var entry = _ghostEntries[i];
                if (!entry.IsSameTarget(resolved.Ghost.BlockId, resolved.WorldCell, resolved.WorldDirection)) entry.SetTarget(resolved.Ghost.BlockId, resolved.WorldCell, resolved.WorldDirection, null);
                
                // 塞がったセルのゴーストだけ不可色へ落とす
                // Only a blocked cell's ghost drops to the not-placeable color
                if (entry.PreviewObject == null) continue;
                var chainBlockSize = MasterHolder.BlockMaster.GetBlockMaster(resolved.Ghost.BlockId).BlockSize;
                var chainPlaceInfo = new PlaceInfo { Position = resolved.WorldCell, Direction = resolved.WorldDirection, BlockId = resolved.Ghost.BlockId };
                var blocked = _existingBlockQuery.IsOverlapping(chainPlaceInfo) || !_groundQuery.IsGroundAligned(resolved.WorldCell, resolved.WorldDirection, chainBlockSize);
                entry.PreviewObject.SetPlaceableColor(!blocked);
            }

            // 連結数が減った分の余剰ゴーストを隠す
            // Hide surplus ghosts left over from a shrunken chain
            for (var i = _resolvedBuffer.Count; i < _ghostEntries.Count; i++) _ghostEntries[i].Hide();
        }
        
        public void Hide()
        {
            foreach (var entry in _ghostEntries) entry.Hide();
        }
    }
}
