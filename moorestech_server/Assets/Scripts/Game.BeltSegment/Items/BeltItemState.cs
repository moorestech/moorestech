namespace Game.BeltSegment
{
    /// <summary>再構築時に受け渡すアイテムと、所属segmentの出口までの距離。</summary>
    public readonly struct BeltItemState
    {
        public readonly BeltItem Item;
        public readonly int DistanceToExit;

        public BeltItemState(BeltItem item, int distanceToExit)
        {
            Item = item;
            DistanceToExit = distanceToExit;
        }
    }
}
