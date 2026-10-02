namespace Core.BeltTransport
{
    // 合流の予約時に搬出可否を問い合わせる。状態は変更しない
    // Queried for output availability during merge reservation. Must not change state
    public interface IBeltSource
    {
        // inputDirectionは問い合わせ側から見た搬入元の方向
        // inputDirection is the source direction seen from the querying side
        bool TryGetOutput(BeltDirection inputDirection);
    }
}
