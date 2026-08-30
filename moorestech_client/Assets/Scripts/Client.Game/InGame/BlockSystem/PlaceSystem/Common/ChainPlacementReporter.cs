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
        
        public static void MarkChainBlockedCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, ChainPlacePreviewState state, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery, bool groundBased, int heightOffset, PlacementFeedback feedback)
        {
            // 連結対象でないブロックは無関係なので素通しする
            // A block that anchors no chain layout is unrelated, so let it pass
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!state.TryGetChain(holdingBlockId, out var chain, out _)) return;
            
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                
                // 連結中はドラッグ複数設置を認めない。同一ドラッグ内の予定地同士は互いを見られないため、カーソルセル1基に限定する
                // No multi-cell drag while chaining: planned cells cannot see each other, so only the cursor cell may place
                if (i != cursorIndex && currentPlaceInfos.Count > 1)
                {
                    placeInfo.Placeable = false;
                    continue;
                }
                
                ChainLayoutResolver.Resolve(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize, chain, existingBlockQuery, groundQuery, groundBased, heightOffset, ResolvedBuffer);
                if (!HasBlockedGhost()) continue;
                
                placeInfo.Placeable = false;
                if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceChainBlocked));
            }
            
            #region Internal
            
            bool HasBlockedGhost()
            {
                foreach (var resolved in ResolvedBuffer)
                {
                    if (resolved.Blocked) return true;
                }
                return false;
            }
            
            #endregion
        }
    }
}
