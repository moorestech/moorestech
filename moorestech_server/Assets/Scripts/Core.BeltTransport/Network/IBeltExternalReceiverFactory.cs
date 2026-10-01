namespace Core.BeltTransport
{
    // サーバーの在庫とクライアントの確定差分が同じ搬出口を実装する。
    // Server inventories and client committed results implement the same output port.
    public interface IBeltExternalReceiverFactory
    {
        IBeltReceiver Create(BeltNetworkConnection connection, int stage);
    }
}
