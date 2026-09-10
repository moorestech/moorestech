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
        
        public static void Resolve(Vector3Int originPosition, BlockDirection placeDirection, Vector3Int anchorBlockSize, IReadOnlyList<ChainGhost> chain, IExistingBlockQuery existingBlockQuery, IChainGroundQuery groundQuery, bool groundBased, int heightOffset, List<ResolvedChainGhost> results)
        {
            results.Clear();
            
            // 設置後にチュートリアルが使う ConvertBlockLocalToWorldCell と同一の換算で解決し、事前検査と実配置のズレを防ぐ
            // Resolve with the same conversion the tutorial uses after placement, so the pre-check and the real layout never disagree
            var footprint = new BlockPositionInfo(originPosition, placeDirection, anchorBlockSize);
            foreach (var ghost in chain)
            {
                var ghostBlockSize = MasterHolder.BlockMaster.GetBlockMaster(ghost.BlockId).BlockSize;
                var worldCell = AnchorRelativeOriginUtil.ResolveWorldOrigin(footprint, ghost.Offset, ghost.LocalDirection, ghostBlockSize);
                var worldDirection = AnchorRelativeDirectionUtil.RotateByAnchor(ghost.LocalDirection, placeDirection);
                var blockReason = ResolveBlockReason(ghost, worldCell, worldDirection, ghostBlockSize);
                results.Add(new ResolvedChainGhost(ghost, worldCell, worldDirection, blockReason));
            }

            #region Internal

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
