using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     カーソルが指すセルと、そこに置くベルトの向き。ブロックの側面に当たったときだけ向きはそのブロックへ差し込む側になる
    ///     The cell under the cursor and the heading a belt there takes; only a hit on a block's side face turns the belt into that block
    /// </summary>
    public readonly struct BeltRouteCursorHit
    {
        public readonly Vector3Int Cell;
        public readonly BlockDirection Direction;

        // 側面・天面・底面のどれかに当たった既存ブロック。地面ヒットはnull
        // The existing block whose face was hit (side, top or bottom); null for a ground hit
        public readonly BlockGameObject HitBlock;

        private BeltRouteCursorHit(Vector3Int cell, BlockDirection direction, BlockGameObject hitBlock)
        {
            Cell = cell;
            Direction = direction;
            HitBlock = hitBlock;
        }

        public static bool TryResolve(Camera camera, int heightOffset, BlockDirection keyDirection, BlockMasterElement holdingMaster, BlockGameObjectDataStore blocks, PlacementFeedback feedback, out BeltRouteCursorHit hit)
        {
            hit = default;
            if (!PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(camera, heightOffset, keyDirection, holdingMaster, feedback, out var cell, out var surfaceType)) return false;

            var direction = surfaceType == null ? keyDirection : ResolveFaceDirection(surfaceType.Value, keyDirection);
            hit = new BeltRouteCursorHit(cell, direction, surfaceType == null ? null : ResolveHitBlock(blocks, cell, surfaceType.Value, direction));
            return true;
        }

        // 面の手前セルからブロックへ流す向き。天面・底面は面の向きが決まらないのでキーの向きのまま
        // The heading that flows from the cell in front of the face into the block; top and bottom faces fix no heading, so the key's stays
        private static BlockDirection ResolveFaceDirection(PreviewSurfaceType surfaceType, BlockDirection keyDirection)
        {
            return surfaceType switch
            {
                PreviewSurfaceType.YX_Origin => BlockDirection.North,
                PreviewSurfaceType.YX_Z => BlockDirection.South,
                PreviewSurfaceType.YZ_Origin => BlockDirection.East,
                PreviewSurfaceType.YZ_X => BlockDirection.West,
                _ => keyDirection,
            };
        }

        // 照準は面の種別しか返さないので、当たった面の奥のセルからブロックを引く。原点以外のセルに当たる大きなブロックは分岐の対象外なのでnullでよい
        // The aim reports only the face kind, so the block is looked up from the cell behind the face; a large block hit off its origin cell is never a branch source, so null is fine
        private static BlockGameObject ResolveHitBlock(BlockGameObjectDataStore blocks, Vector3Int cell, PreviewSurfaceType surfaceType, BlockDirection direction)
        {
            var behindFace = surfaceType switch
            {
                PreviewSurfaceType.XZ_Y => cell + Vector3Int.down,
                PreviewSurfaceType.XZ_Origin => cell + Vector3Int.up,
                _ => cell + BeltRouteDirection.Forward(direction),
            };
            return blocks.TryGetBlockGameObject(behindFace, out var block) ? block : null;
        }
    }
}
