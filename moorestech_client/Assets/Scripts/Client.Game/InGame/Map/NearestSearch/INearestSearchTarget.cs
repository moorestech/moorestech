using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     最寄り探索の対象。座標は索引の構築時に1度だけ読まれ、探索可否は探索のたびに読まれる
    ///     Target of nearest search; the position is read once when the index is built, searchability on every search
    /// </summary>
    internal interface INearestSearchTarget
    {
        // メソッド名で「焼き込み用に1度だけ読む座標」であることを表明する（毎フレームの実位置ではない）
        // The method name states this is the position baked once at build time, not the live per-frame one
        Vector3 GetIndexPosition();

        // 索引が返してよい対象かどうか。木を組み直さずに墓標としてスキップするため探索時に読む
        // Whether the index may return this target; read at search time so it can be skipped as a tombstone without a rebuild
        bool IsSearchable { get; }
    }
}
