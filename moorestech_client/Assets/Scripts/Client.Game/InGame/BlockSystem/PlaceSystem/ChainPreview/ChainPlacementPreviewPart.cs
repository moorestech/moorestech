using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.Control;
// ワールドピン配信の共有基盤
// Shared world-pin publication store: a web-presentation port, not tutorial logic
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     設置カーソルへ連結ゴースト群を追従表示する
    ///     Follows the placement cursor with the chain ghosts; blocked cells turn the not-placeable color, and world pins go to the web UI
    /// </summary>
    public class ChainPlacementPreviewPart
    {
        private readonly ChainPlacePreviewState _state;
        private readonly IExistingBlockQuery _existingBlockQuery;
        private readonly IChainGroundQuery _groundQuery;
        private readonly List<PlacementGhostEntry> _ghostEntries = new();
        private readonly List<ChainLayoutResolver.ResolvedChainGhost> _resolvedBuffer = new();
        
        public ChainPlacementPreviewPart(ChainPlacePreviewState state, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery)
        {
            _state = state;
            _existingBlockQuery = existingBlockQuery;
            _groundQuery = groundQuery;
        }
        
        public void Apply(PlaceInfo cursorPlaceInfo, BlockMasterElement holdingBlockMaster, bool groundBased, int heightOffset)
        {
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!_state.TryGetChain(holdingBlockId, out var chain, out var tutorialGuid) || chain.Count == 0)
            {
                Hide();
                return;
            }
            
            ChainLayoutResolver.Resolve(cursorPlaceInfo.Position, cursorPlaceInfo.Direction, holdingBlockMaster.BlockSize, chain, _existingBlockQuery, _groundQuery, groundBased, heightOffset, _resolvedBuffer);
            for (var i = 0; i < _resolvedBuffer.Count; i++)
            {
                var resolved = _resolvedBuffer[i];
                if (_ghostEntries.Count <= i) _ghostEntries.Add(new PlacementGhostEntry($"chain-preview-pin-{i}"));
                
                var entry = _ghostEntries[i];
                entry.SetTarget(resolved.Ghost.BlockId, resolved.WorldCell, resolved.WorldDirection, null);
                
                // 塞がったセルのゴーストを不可色にする
                // Only a blocked cell's ghost drops to the not-placeable color; Blocked was decided at resolution
                if (entry.PreviewObject != null) entry.PreviewObject.SetPlaceableColor(!resolved.Blocked);
                
                PublishWebPin(entry, tutorialGuid);
            }
            
            // 連結件数が減ったら余剰ゴーストを畳む
            // Fold surplus ghosts on the frame the chain shrinks
            for (var i = _resolvedBuffer.Count; i < _ghostEntries.Count; i++) HideEntry(_ghostEntries[i]);
            
            #region Internal
            
            // WebUIモードではワールドピンも配信する
            // In web UI mode, publish world pins the same way the sibling tutorial ghosts do
            void PublishWebPin(PlacementGhostEntry entry, System.Guid ownerTutorialGuid)
            {
                if (!WebUiScreenGate.IsWebUiMode || entry.PreviewObject == null) return;
                
                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;
                
                var projection = WorldPinScreenProjection.Project(camera, entry.PreviewObject.transform.position);
                WorldPinStateStore.Instance.SetPin(entry.WebPinId, ownerTutorialGuid.ToString("D"), projection);
            }
            
            #endregion
        }
        
        public void Hide()
        {
            foreach (var entry in _ghostEntries) HideEntry(entry);
        }
        
        private static void HideEntry(PlacementGhostEntry entry)
        {
            entry.Hide();
            WorldPinStateStore.Instance.RemovePin(entry.WebPinId);
        }
    }
}
