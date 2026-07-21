using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    public struct WorldPinProjection
    {
        public float ScreenX;
        public float ScreenY;
        public bool OnScreen;
        public float DirectionX;
        public float DirectionY;
    }

    /// <summary>
    /// ワールド座標をWeb UI向けの正規化スクリーン座標へ射影する。座標の正はUnity側で、Webは受信値を描くだけ。
    /// Projects a world position into normalized screen coordinates for the Web UI; Unity owns the math, the web only renders.
    /// </summary>
    public static class WorldPinScreenProjection
    {
        public static WorldPinProjection Project(Camera camera, Vector3 worldPosition)
        {
            // ビューポート射影で判定
            // Judge via viewport projection
            var viewportPos = camera.WorldToViewportPoint(worldPosition);
            var onScreen = 0 < viewportPos.z &&
                           0 <= viewportPos.x && viewportPos.x <= 1 &&
                           0 <= viewportPos.y && viewportPos.y <= 1;

            // カメラ背面でも破綻しないよう、方向はカメラローカル軸への投影で求める（HudArrowと同じ手法）
            // Derive the direction via camera-local axis projection so behind-camera targets stay stable (same as HudArrow)
            var cameraToTarget = worldPosition - camera.transform.position;
            var localX = Vector3.Dot(cameraToTarget, camera.transform.right);
            var localY = Vector3.Dot(cameraToTarget, camera.transform.up);
            var direction = new Vector2(localX, localY).normalized;

            // カメラ前後軸上の縮退（方向ゼロ）は画面下方向へフォールバックし、矢印の中央表示を防ぐ
            // Degenerate targets on the camera's forward axis fall back to screen-down so the arrow never sits at center
            if (direction == Vector2.zero) direction = Vector2.down;

            return new WorldPinProjection
            {
                // CSS座標系へ変換
                // Convert into CSS axes
                ScreenX = viewportPos.x,
                ScreenY = 1f - viewportPos.y,
                OnScreen = onScreen,
                DirectionX = direction.x,
                DirectionY = -direction.y,
            };
        }
    }
}
