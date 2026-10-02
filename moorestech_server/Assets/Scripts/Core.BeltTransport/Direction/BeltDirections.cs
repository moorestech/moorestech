namespace Core.BeltTransport
{
    public static class BeltDirections
    {
        // 0と1、2と3を反対方向の組にしているのでbit反転で求まる
        // 0/1 and 2/3 are opposite pairs, so flipping the lowest bit gives the opposite
        public static BeltDirection Opposite(BeltDirection direction)
        {
            return (BeltDirection)((int)direction ^ 1);
        }
    }
}
