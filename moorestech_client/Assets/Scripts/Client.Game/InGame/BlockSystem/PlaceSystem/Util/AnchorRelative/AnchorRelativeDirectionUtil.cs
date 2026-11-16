using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative
{
    /// <summary>
    ///     ローカル向きをアンカー向きで回しワールド向きへ変換
    ///     Maps an anchor-North-basis local direction into world space using the placed anchor's direction
    /// </summary>
    public static class AnchorRelativeDirectionUtil
    {
        private const int HorizontalDirectionCount = 4;

        public static BlockDirection RotateByAnchor(BlockDirection localDirection, BlockDirection anchorDirection)
        {
            // 水平4方向どうしの合成は90度刻みの回転の足し算になり、常に4方向へ収まる
            // Composing two horizontal headings adds 90-degree steps, so the result always stays within the four directions
            return (BlockDirection)(((int)localDirection + (int)anchorDirection) % HorizontalDirectionCount);
        }
    }
}
