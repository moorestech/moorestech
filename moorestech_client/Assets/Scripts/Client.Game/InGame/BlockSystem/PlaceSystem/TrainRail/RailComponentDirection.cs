using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    /// <summary>
    /// ユーザー側からの見た目はこの方向でレールが設置される。実態はVector3で表現される。
    /// Rail is placed in this direction as seen from the user. The actual representation is in Vector3.
    /// </summary>
    public enum RailComponentDirection
    {
        Direction0,
        Direction45,
        Direction90,
        Direction135,
        Direction180,
        Direction225,
        Direction270,
        Direction315,
    }
    
    public static class RailComponentDirectionExtension
    {
        public static Vector3 ToVector3(this RailComponentDirection direction)
        {
            return direction switch
            {
                RailComponentDirection.Direction0 => new Vector3(1, 0, 0),
                RailComponentDirection.Direction45 => new Vector3(1, 0, 1).normalized,
                RailComponentDirection.Direction90 => new Vector3(0, 0, 1),
                RailComponentDirection.Direction135 => new Vector3(-1, 0, 1).normalized,
                RailComponentDirection.Direction180 => new Vector3(-1, 0, 0),
                RailComponentDirection.Direction225 => new Vector3(-1, 0, -1).normalized,
                RailComponentDirection.Direction270 => new Vector3(0, 0, -1),
                RailComponentDirection.Direction315 => new Vector3(1, 0, -1).normalized,
                _ => Vector3.zero
            };
        }
    }
}