using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     索引テスト用の座標だけを持つターゲット
    ///     Position-only target for index tests
    /// </summary>
    public sealed class NearestSearchTestTarget : INearestSearchTarget
    {
        public Vector3 Position { get; }

        public NearestSearchTestTarget(Vector3 position)
        {
            Position = position;
        }
    }
}
