namespace Game.CleanRoom
{
    // 部屋の運用段階。猶予で flicker を吸収する。
    // Operational status of a room; grace absorbs flicker.
    public enum CleanRoomRoomStatus
    {
        Valid,
        Degraded,
        Invalid,
    }
}
