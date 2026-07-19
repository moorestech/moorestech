namespace Server.Event
{
    // プレイヤー1接続分のイベント送信先
    // Per-connection sink that receives events for one player
    public interface IPlayerEventSink
    {
        // イベント1件を接続の送信キューへ積む
        // Enqueue one event into the connection's send queue
        void EnqueueEvent(EventMessagePack eventMessagePack);
    }
}
