using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 設置モードのプレビュー表示の距離カリング。プレイヤー周囲20mのブロックだけ描画する
    /// Preview distance culling for build mode: only blocks within 20m of the player render
    /// </summary>
    public sealed class BlockPreviewRadiusCullingManager : ITickable
    {
        // 半径20mはユーザー裁定。チャンク4mで超過を最大約5.7mに抑え、再評価は1m移動ごと
        // 20m radius is a user ruling; 4m chunks cap overshoot near 5.7m, re-evaluated per 1m of movement
        private const float ChunkSize = 4f;
        private const float ActiveRadius = 20f;
        private const float ReevalMoveDistance = 1f;

        public readonly PlayerCenteredDistanceCulling Culling;

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
