using System.Collections.Generic;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     設置中ブロックの原点と向きから、連結ゴースト群のワールドセル・向きを解決する
    ///     Resolves chain ghosts' world cells and directions from the being-placed block's origin and direction
    /// </summary>
    public static class ChainLayoutResolver
    {
        public readonly struct ResolvedChainGhost
        {
            public readonly ChainGhost Ghost;
            public readonly Vector3Int WorldCell;
            public readonly BlockDirection WorldDirection;
            
            public ResolvedChainGhost(ChainGhost ghost, Vector3Int worldCell, BlockDirection worldDirection)
            {
                Ghost = ghost;
                WorldCell = worldCell;
                WorldDirection = worldDirection;
            }
        }
        
        public static void Resolve(Vector3Int originPosition, BlockDirection placeDirection, Vector3Int anchorBlockSize, IReadOnlyList<ChainGhost> chain, List<ResolvedChainGhost> results)
        {
            results.Clear();
            
            // 設置後にチュートリアルが使う ConvertBlockLocalToWorldCell と同一の換算で解決し、事前検査と実配置のズレを防ぐ
            // Resolve with the same conversion the tutorial uses after placement, so the pre-check and the real layout never disagree
            var footprint = new BlockPositionInfo(originPosition, placeDirection, anchorBlockSize);
            foreach (var ghost in chain)
            {
                var worldCell = footprint.ConvertBlockLocalToWorldCell(ghost.Offset);
                var worldDirection = AnchorRelativeDirectionUtil.RotateByAnchor(ghost.LocalDirection, placeDirection);
                results.Add(new ResolvedChainGhost(ghost, worldCell, worldDirection));
            }
        }
    }
}
