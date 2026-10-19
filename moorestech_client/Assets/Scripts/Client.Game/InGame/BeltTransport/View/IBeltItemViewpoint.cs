using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // ベルト上アイテムの具現化範囲の基準点。本番はメインカメラ、テストは固定座標
    // The reference point for the belt item materialization range: the main camera in production, a fixed position in tests
    public interface IBeltItemViewpoint
    {
        // 基準点が無い間(カメラ未生成)は false。そのときは何も具現化しない
        // False while there is no reference point yet (camera not created); nothing is materialized then
        bool TryGetPosition(out Vector3 position);
    }
}
