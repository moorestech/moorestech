using Client.Game.InGame.DistanceCulling;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 半径内/外の指示をブロックのプレビュー表示へ渡す
    /// Forwards within/outside-radius instructions to a block's preview display
    /// </summary>
    public sealed class BlockPreviewRadiusCullingTarget : IDistanceCullingTarget
    {
        private readonly BlockGameObject _block;

        internal BlockPreviewRadiusCullingTarget(BlockGameObject block)
        {
            _block = block;
        }

        public void SetWithinRange(bool withinRange)
        {
            _block.SetPreviewOnlyObjectsWithinPlayerRadius(withinRange);
        }
    }
}
