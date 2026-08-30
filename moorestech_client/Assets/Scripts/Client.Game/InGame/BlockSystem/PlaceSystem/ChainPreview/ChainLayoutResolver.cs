using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     設置中ブロックの原点と向きから、連結ゴースト群のワールドセル・向き・可否を一度に解決する
    ///     Resolves chain ghosts' world cells, directions and blocked flags in one pass from the being-placed block
    /// </summary>
    public static class ChainLayoutResolver
    {
        public readonly struct ResolvedChainGhost
        {
            public readonly ChainGhost Ghost;
            public readonly Vector3Int WorldCell;
            public readonly BlockDirection WorldDirection;
            public readonly Vector3Int BlockSize;
            
            // 可否は解決時に一度だけ確定し、設置判定とゴースト色が定義上一致する
            // Blocked is decided once at resolution, so the placement check and the ghost color agree by definition
            public readonly bool Blocked;
            
            public ResolvedChainGhost(ChainGhost ghost, Vector3Int worldCell, BlockDirection worldDirection, Vector3Int blockSize, bool blocked)
            {
                Ghost = ghost;
                WorldCell = worldCell;
                WorldDirection = worldDirection;
                BlockSize = blockSize;
                Blocked = blocked;
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
                var blocked = IsBlocked(ghost, worldCell, worldDirection, ghostBlockSize);
                results.Add(new ResolvedChainGhost(ghost, worldCell, worldDirection, ghostBlockSize, blocked));
            }
            
            #region Internal
            
            // 既存ブロックの重なり、または地表との不整合（埋まり/浮き）で不成立。ブロック面スタック設置中は地表基準が無いので地形は見ない
            // Blocked by an existing block overlap or misaligned ground; block-face stacking has no ground basis, so terrain is skipped there
            bool IsBlocked(ChainGhost ghost, Vector3Int worldCell, BlockDirection worldDirection, Vector3Int ghostBlockSize)
            {
                var chainPlaceInfo = new PlaceInfo { Position = worldCell, Direction = worldDirection, BlockId = ghost.BlockId };
                if (existingBlockQuery.IsOverlapping(chainPlaceInfo)) return true;
                return groundBased && !groundQuery.IsGroundAligned(worldCell, worldDirection, ghostBlockSize, heightOffset);
            }
            
            #endregion
        }
    }
}
