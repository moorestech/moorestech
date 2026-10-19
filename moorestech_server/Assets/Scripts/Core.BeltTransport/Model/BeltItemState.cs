namespace Core.BeltTransport
{
    // 再構築時に受け渡すアイテムと、所属segmentの出口までの距離
    // An item handed over on rebuild, with its distance to the owning segment's exit
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
