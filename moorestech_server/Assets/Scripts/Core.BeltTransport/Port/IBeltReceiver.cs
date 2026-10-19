namespace Core.BeltTransport
{
    // 接続はtick境界で登録する。TryReceiveは成功した場合だけアイテムを受け取り、搬送元は成功後に自分のアイテムを消す
    // 複数の搬送元を持つ機械は、並列の問い合わせと搬入を自分で同期する。inputDirectionは全て受け入れ側から見た搬入元の方向
    // Connections are registered at tick boundaries. TryReceive takes the item only on success; the sender removes its own item afterwards
    // Machines with multiple senders synchronize parallel queries and receives themselves. Every inputDirection is the source direction seen from the receiver
    public interface IBeltReceiver
    {
        void AttachInput(IBeltSource source, BeltDirection inputDirection);

        // 受け入れ可能な進入距離。0以下なら搬入不可
        // Acceptable entry length; zero or less means not acceptable
        int GetOffer(BeltDirection inputDirection);

        // lengthは進入距離で、搬送元が1～ItemWidthに収める
        // length is the entry length, kept within 1..ItemWidth by the sender
        bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item);
    }
}
