using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     索引テスト用の座標だけを持つターゲット。探索可否は個別に切り替えられる
    ///     Position-only target for index tests, with searchability that can be toggled per instance
    /// </summary>
    internal sealed class NearestSearchTestTarget : INearestSearchTarget
    {
        public Vector3 Position { get; }
        public bool IsSearchable { get; private set; } = true;

        public NearestSearchTestTarget(Vector3 position)
        {
            Position = position;
        }

        public Vector3 GetIndexPosition()
        {
            return Position;
        }

        public void SetSearchable(bool searchable)
        {
            IsSearchable = searchable;
        }
    }
}
