using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     最寄り探索の対象。索引は座標だけを知り、可否や破壊状態は利用側が判断する
    ///     Target of nearest search; the index knows only the position, availability is decided by the caller
    /// </summary>
    public interface INearestSearchTarget
    {
        Vector3 Position { get; }
    }
}
