using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// - コライダーの有効状態を制御
    /// - プレイヤー周囲120mが基準
    /// - Control collider activation
    /// - Use a 120m radius around the player
    /// </summary>
    public sealed class ColliderDistanceCullingManager : ITickable
    {
        // 有効半径はブロック選択レイ(100m)+カメラ最大距離(10m)+再評価移動量(8m)を包含
        // Active radius covers pick ray (100m) + max camera boom (10m) + re-eval move (8m)
        private const float ChunkSize = 32f;
        private const float ActiveRadius = 120f;
        private const float ReevalMoveDistance = 8f;

        internal readonly PlayerCenteredDistanceCulling Culling;

        public ColliderDistanceCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            Culling = new PlayerCenteredDistanceCulling(playerSystemContainer, ChunkSize, ActiveRadius, ReevalMoveDistance);
        }

        public void Tick()
        {
            Culling.Tick();
        }
    }
}
