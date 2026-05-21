namespace Game.PlayerRiding.Interface
{
    // 乗車/降車要求の結果。RideActionProtocol のレスポンスにも使う（Phase 3）。
    // Result of a ride/dismount request. Also used in the RideActionProtocol response (Phase 3).
    public enum RideActionResult : byte
    {
        Success,
        NoSeatAvailable,
        RidableNotFound,
        AlreadyRiding,
        NotRiding,
    }
}
