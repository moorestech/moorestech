using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// - 設置モードの表示制御
    /// - プレイヤー周囲20mが基準
    /// - Control build-mode preview display
    /// - Use a 20m radius around the player
    /// </summary>
    public sealed class BlockPreviewRadiusCullingManager : ITickable
    {
        // 半径20mはユーザー裁定。チャンク4mで超過を最大約5.7mに抑え、再評価は1m移動ごと
        // 20m radius is a user ruling; 4m chunks cap overshoot near 5.7m, re-evaluated per 1m of movement
        private const float ChunkSize = 4f;
        private const float ActiveRadius = 20f;
        private const float ReevalMoveDistance = 1f;

        internal readonly PlayerCenteredDistanceCulling Culling;

        public BlockPreviewRadiusCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            Culling = new PlayerCenteredDistanceCulling(playerSystemContainer, ChunkSize, ActiveRadius, ReevalMoveDistance);
        }

        public void Tick()
        {
            Culling.Tick();
        }
    }
}
