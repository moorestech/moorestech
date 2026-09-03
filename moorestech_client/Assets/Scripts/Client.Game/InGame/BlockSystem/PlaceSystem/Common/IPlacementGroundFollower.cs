using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     ドラッグ列のYを地形へ追従させる窓口。実装は地表探査を持つGround層側にある
    ///     The port that makes a drag run's Y follow the terrain; the implementation lives in the Ground layer that owns ground probing
    /// </summary>
    public interface IPlacementGroundFollower
    {
        void FollowGround(PlacementRun run, PlacementHitSurfaceKind surfaceKind, Vector3Int blockSize, int heightOffset);
    }
}
