using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     カーソルが指すセルと、そこに置くベルトの向き。ブロックの側面に当たったときの向きは BeltRouteEndpointFacing が決める
    ///     The cell under the cursor and the heading a belt there takes; a hit on a block's side face is resolved by BeltRouteEndpointFacing
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

        public static bool TryResolve(Camera camera, int heightOffset, BlockDirection keyDirection, BlockMasterElement holdingMaster, BlockGameObjectDataStore blocks, IBeltRouteWorld world, BeltRouteEndpointRole role, PlacementFeedback feedback, out BeltRouteCursorHit hit)
        {
            hit = default;
            if (!PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(camera, heightOffset, keyDirection, holdingMaster, feedback, out var cell, out var surfaceType)) return false;

            // 地面・天面・底面はキーの向き
            // Ground, top and bottom keep the key's heading
            if (surfaceType == null)
            {
                hit = new BeltRouteCursorHit(cell, keyDirection, null);
                return true;
            }
            var isSideFace = TryGetFaceInward(surfaceType.Value, out var faceInward);
            var direction = isSideFace ? BeltRouteEndpointFacing.Resolve(world, cell, faceInward, keyDirection, role) : keyDirection;
            hit = new BeltRouteCursorHit(cell, direction, ResolveHitBlock(blocks, cell, surfaceType.Value, faceInward));
            return true;
        }

        // 側面なら、面の手前セルからブロックへ向かう向きを返す
        // For a side face, returns the heading from the cell in front of the face into the block
        private static bool TryGetFaceInward(PreviewSurfaceType surfaceType, out BlockDirection faceInward)
        {
            faceInward = BlockDirection.North;
            switch (surfaceType)
            {
                case PreviewSurfaceType.YX_Origin: faceInward = BlockDirection.North; return true;
                case PreviewSurfaceType.YX_Z: faceInward = BlockDirection.South; return true;
                case PreviewSurfaceType.YZ_Origin: faceInward = BlockDirection.East; return true;
                case PreviewSurfaceType.YZ_X: faceInward = BlockDirection.West; return true;
                default: return false;
            }
        }

        // 照準は面の種別しか返さないので、当たった面の奥のセルからブロックを引く。原点以外のセルに当たる大きなブロックは分岐の対象外なのでnullでよい
        // The aim reports only the face kind, so the block is looked up from the cell behind the face; a large block hit off its origin cell is never a branch source, so null is fine
        private static BlockGameObject ResolveHitBlock(BlockGameObjectDataStore blocks, Vector3Int cell, PreviewSurfaceType surfaceType, BlockDirection faceInward)
        {
            var behindFace = surfaceType switch
            {
                PreviewSurfaceType.XZ_Y => cell + Vector3Int.down,
                PreviewSurfaceType.XZ_Origin => cell + Vector3Int.up,
                _ => cell + BeltRouteDirection.Forward(faceInward),
            };
            return blocks.TryGetBlockGameObject(behindFace, out var block) ? block : null;
        }
    }
}
