// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation.
namespace Core.BeltTransport
{
    /// <summary>合流の予約時に搬出可否を問い合わせる。状態は変更しない。</summary>
    internal interface IBeltSource
    {
        /// <param name="inputDirection">問い合わせ側から見た搬入元の方向。</param>
        bool TryGetOutput(BeltDirection inputDirection);
    }

    /// <summary>
    /// 接続はtick境界で登録する。TryReceiveは成功した場合だけアイテムを受け取る。
    /// 搬送元が所有するアイテムの削除は、搬送元が成功後に行う。
    /// Connections change between ticks; ownership transfers only after successful receipt.
    /// </summary>
    public interface IBeltReceiver
    {
        /// <summary>受け入れ可能な進入距離を返す。0以下なら搬入不可。</summary>
        /// <param name="inputDirection">受け入れ側から見た搬入元の方向。</param>
        int GetOffer(BeltDirection inputDirection);
        /// <param name="inputDirection">受け入れ側から見た搬入元の方向。</param>
        /// <param name="length">進入距離。搬送元が1～ItemWidthに収める。</param>
        bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item);
    }

    internal enum BeltSegmentKind
    {
        Normal,
        Merge,
        Branch
    }
}
