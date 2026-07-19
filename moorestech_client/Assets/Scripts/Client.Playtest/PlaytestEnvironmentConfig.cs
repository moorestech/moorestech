using UnityEngine;

namespace Client.Playtest
{
    public class PlaytestEnvironmentConfig
    {
        // 建築を全解放し無料で設置できるようにする（ビルドメニュー全表示 + クライアント/サーバー両方のコスト消費スキップ）
        // Unlock everything and make placement free (full build menu + cost skip on both client and server)
        public bool FreeBlockPlacement = true;

        // y=32上面の平坦足場を生成する（無限落下防止・UI設置レイキャストの前提）
        // Create the flat scaffold with its top at y=32 (prevents infinite falls; required by UI-placement raycasts)
        public bool CreateFlatGround = true;

        // 初期ワープ先。CreateFlatGround時は足場中央上空がデフォルト
        // Initial warp position; defaults to just above the scaffold center when CreateFlatGround is on
        public Vector3 SpawnPosition = new(0f, 33.5f, 0f);
    }
}
