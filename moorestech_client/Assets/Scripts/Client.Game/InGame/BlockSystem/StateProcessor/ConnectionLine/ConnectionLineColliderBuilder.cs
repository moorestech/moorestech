using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     接続線の当たり判定を生成する共通部品
    ///     Shared builder for connection-line hit colliders
    /// </summary>
    public static class ConnectionLineColliderBuilder
    {
        // directionの1はローカルY軸
        // Direction 1 is the local Y axis
        private const int CapsuleDirectionYAxis = 1;

        public static void AddCapsule(Transform parent, Vector3 center, Vector3 axisDir, float radius, float length)
        {
            // 専用レイヤに置き、既存のブロック操作レイキャストへの干渉を防ぐ
            // Place on the dedicated layer to avoid interfering with existing block-operation raycasts
            var colliderObject = new GameObject("ConnectionLineCollider");
            colliderObject.layer = LayerConst.ConnectionLineLayer;
            colliderObject.transform.SetParent(parent, false);

            // カプセルのY軸を線方向へ向ける
            // Orient the capsule's Y axis along the line
            colliderObject.transform.position = center;
            colliderObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, axisDir);

            // トリガー化してプレイヤーとの物理衝突を防ぐ（レイキャストにはヒットする）
            // Make it a trigger to avoid physical collision with the player (still hit by raycasts)
            var capsule = colliderObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = CapsuleDirectionYAxis;
            capsule.radius = radius;
            capsule.height = length;
        }
    }
}
