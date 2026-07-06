using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     足場生成とプレイヤーワープの定型処理。毎セッション再発明されていた「板＋Warp」を1メソッドに集約する
    ///     Standard scaffold + warp helpers, consolidating the per-session reinvented "plate + warp" pattern
    /// </summary>
    public static class PlaytestSetup
    {
        private const string GroundObjectName = "PlaytestFlatGround";

        // kill floor(y<-50)に落ちない高さに足場を置く過去セッションの定番座標
        // Battle-tested scaffold placement that stays clear of the kill floor (y < -50)
        public static readonly Vector3 GroundCenter = new(0, 30, 0);
        public static readonly Vector3 GroundSize = new(50, 3, 50);

        public static async UniTask CreateFlatGroundAndWarp()
        {
            CreateFlatGround();

            // 足場上面の中央へ少し浮かせてワープし、着地を数フレーム待つ
            // Warp slightly above the scaffold's top center, then wait a few frames to land
            var warpPosition = new Vector3(GroundCenter.x, GroundCenter.y + GroundSize.y / 2f + 1.5f, GroundCenter.z);
            WarpPlayer(warpPosition);
            await UniTask.DelayFrame(10);
        }

        public static GameObject CreateFlatGround()
        {
            // 既存の足場があれば再利用する（多重生成防止）
            // Reuse an existing scaffold if present (prevents duplicates)
            var existing = GameObject.Find(GroundObjectName);
            if (existing != null) return existing;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = GroundObjectName;
            ground.transform.position = GroundCenter;
            ground.transform.localScale = GroundSize;
            return ground;
        }

        public static void WarpPlayer(Vector3 position)
        {
            // クライアント側の位置を移動し、サーバーにも同期する
            // Move the client-side player and sync the position to the server
            PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(position);
            ClientContext.VanillaApi.SendOnly.SendPlayerPosition(position);
        }
    }
}
