using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// - 歯車回転の更新範囲を制御
    /// - プレイヤー周囲50mが基準
    /// - Control which gears animate rotation
    /// - Use a 50m radius around the player
    /// </summary>
    public sealed class GearRotationCullingManager : ITickable
    {
        // 半径50mはユーザー裁定。チャンク8mで超過を最大約11mに抑え、再評価は2m移動ごと
        // 50m radius is a user ruling; 8m chunks cap overshoot near 11m, re-evaluated per 2m of movement
        private const float ChunkSize = 8f;
        private const float ActiveRadius = 50f;
        private const float ReevalMoveDistance = 2f;

        internal readonly PlayerCenteredDistanceCulling Culling;

        public GearRotationCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            Culling = new PlayerCenteredDistanceCulling(playerSystemContainer, ChunkSize, ActiveRadius, ReevalMoveDistance);
        }

        public void Tick()
        {
            Culling.Tick();
        }
    }
}
