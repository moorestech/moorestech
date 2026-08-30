using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     連結レイアウト（風車＋シャフト＋粉砕機等）の全セルが置けない設置を不可にする。鉱脈制限と同じくクライアント側のみの制限
    ///     Rejects placements whose chain layout cells cannot all fit; a client-side-only restriction like the vein limit
    /// </summary>
    public static class ChainPlacementReporter
    {
        // 解決結果の使い回しバッファ。設置プレビューは毎フレーム回る
        // Reused resolution buffer; the placement preview runs every frame
        private static readonly List<ChainLayoutResolver.ResolvedChainGhost> ResolvedBuffer = new();
        
        public static void MarkChainBlockedCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, ChainPlacePreviewState state, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery, PlacementFeedback feedback)
        {
            // 連結対象でないブロックは無関係なので素通しする
            // A block that anchors no chain layout is unrelated, so let it pass
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!state.TryGetChain(holdingBlockId, out var chain)) return;
            
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                if (!IsChainPlaceable(placeInfo)) 
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceChainBlocked));
                }
            }
            
            #region Internal
            
            // 連結セルが1つでも塞がっていれば不成立
            // The layout fails when any chain cell is blocked by an existing block or misaligned ground
            bool IsChainPlaceable(PlaceInfo placeInfo)
            {
                ChainLayoutResolver.Resolve(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize, chain, ResolvedBuffer);
                foreach (var resolved in ResolvedBuffer)
                {
                    var chainBlockSize = MasterHolder.BlockMaster.GetBlockMaster(resolved.Ghost.BlockId).BlockSize;
                    var chainPlaceInfo = new PlaceInfo { Position = resolved.WorldCell, Direction = resolved.WorldDirection, BlockId = resolved.Ghost.BlockId };
                    if (existingBlockQuery.IsOverlapping(chainPlaceInfo)) return false;
                    if (!groundQuery.IsGroundAligned(resolved.WorldCell, resolved.WorldDirection, chainBlockSize)) return false;
                }
                return true;
            }
            
            #endregion
        }
    }
}
