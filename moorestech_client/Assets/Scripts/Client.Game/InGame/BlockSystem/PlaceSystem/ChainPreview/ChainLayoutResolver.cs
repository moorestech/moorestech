using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結ゴースト群のセル・向き・可否を解決する
    ///     Resolves the chain ghosts' world cells, directions and blocked flags in one pass
    /// </summary>
    public static class ChainLayoutResolver
    {
        public readonly struct ResolvedChainGhost
        {
            public readonly ChainGhost Ghost;
            public readonly Vector3Int WorldCell;
            public readonly BlockDirection WorldDirection;

            // 不可の原因は解決時に一度だけ確定し、設置判定・ゴースト色・文言が定義上一致する
            // The block reason is decided once at resolution, so the placement check, the ghost color and the wording agree by definition
            public readonly ChainCellBlockReason BlockReason;

            public ResolvedChainGhost(ChainGhost ghost, Vector3Int worldCell, BlockDirection worldDirection, ChainCellBlockReason blockReason)
            {
                Ghost = ghost;
                WorldCell = worldCell;
                WorldDirection = worldDirection;
                BlockReason = blockReason;
            }
        }
        
        // 戻り値は設置全体の不可原因（None=置ける）。12方位で表せないゴーストがあれば VerticalAnchor を返し results は空にする
        // Returns the placement-wide block reason (None = placeable); an unrepresentable ghost yields VerticalAnchor with results left empty
        public static ChainCellBlockReason Resolve(Vector3Int originPosition, BlockDirection placeDirection, Vector3Int anchorBlockSize, IReadOnlyList<ChainGhost> chain, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery, bool groundBased, int heightOffset, List<ResolvedChainGhost> results)
        {
            results.Clear();

            // 設置後にチュートリアルが使う ConvertBlockLocalToWorldCell と同一の換算で解決し、事前検査と実配置のズレを防ぐ
            // Resolve with the same conversion the tutorial uses after placement, so the pre-check and the real layout never disagree
            var footprint = new BlockPositionInfo(originPosition, placeDirection, anchorBlockSize);
            foreach (var ghost in chain)
            {
                var ghostBlockSize = MasterHolder.BlockMaster.GetBlockMaster(ghost.BlockId).BlockSize;
                var worldCell = AnchorRelativeOriginUtil.ResolveWorldOrigin(footprint, ghost.Offset, ghost.LocalDirection, ghostBlockSize);

                // 上下向き設置では向きが12方位に収まらないゴーストがあり、1件でもあればレイアウトごと不可にする
                // Facing up/down can push a ghost outside the 12 directions; one such ghost rejects the whole layout
                if (!AnchorRelativeDirectionUtil.TryRotateByAnchor(ghost.LocalDirection, placeDirection, out var worldDirection))
                {
                    results.Clear();
                    return ChainCellBlockReason.VerticalAnchor;
                }

                var blockReason = ResolveBlockReason(ghost, worldCell, worldDirection, ghostBlockSize);
                results.Add(new ResolvedChainGhost(ghost, worldCell, worldDirection, blockReason));
            }

            return FindFirstBlockReason();

            #region Internal

            // 先に見つかった不可セルの原因を設置全体の原因にする
            // The first blocked cell's reason becomes the placement-wide reason
            ChainCellBlockReason FindFirstBlockReason()
            {
                foreach (var resolved in results)
                {
                    if (resolved.BlockReason != ChainCellBlockReason.None) return resolved.BlockReason;
                }
                return ChainCellBlockReason.None;
            }

            // 既存ブロックの重なりを先に見て、次に地表との不整合（地表なし/高さ不一致）を見る。ブロック面スタック設置中は地表基準が無いので地形は見ない
            // Check the existing block overlap first, then the ground mismatch (missing ground or height gap); block-face stacking has no ground basis, so terrain is skipped there
            ChainCellBlockReason ResolveBlockReason(ChainGhost ghost, Vector3Int worldCell, BlockDirection worldDirection, Vector3Int ghostBlockSize)
            {
                var chainPlaceInfo = new PlaceInfo { Position = worldCell, Direction = worldDirection, BlockId = ghost.BlockId };
                if (existingBlockQuery.IsOverlapping(chainPlaceInfo)) return ChainCellBlockReason.OverlappingBlock;
                if (!groundBased) return ChainCellBlockReason.None;
                return groundQuery.ResolveGroundAlignment(worldCell, worldDirection, ghostBlockSize, heightOffset);
            }
            
            #endregion
        }
    }
}
