namespace Core.BeltTransport
{
    // segmentから1個搬出が成立した瞬間の通知先。handleはSetExitObserverで渡された値をそのまま返す
    // Notified the moment one item is sent out of a segment; handle is the value given through SetExitObserver
    // 搬出は1tickに最大1回なので、受け手は1tick内で2回呼ばれない前提で数えてよい
    // Output happens at most once per tick, so a receiver may count on never being called twice within a tick
    public interface IBeltSegmentExitObserver
    {
        void OnExited(int handle);
    }
}
