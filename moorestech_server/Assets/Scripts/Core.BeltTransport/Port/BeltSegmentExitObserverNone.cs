namespace Core.BeltTransport
{
    // 搬出を誰も見ないときの既定。サーバーの搬送はこれのままで、nullチェックを搬出経路に置かない
    // The default when nobody watches outputs; the server's transport keeps it, so the output path carries no null check
    public sealed class BeltSegmentExitObserverNone : IBeltSegmentExitObserver
    {
        public static readonly BeltSegmentExitObserverNone Instance = new();

        private BeltSegmentExitObserverNone()
        {
        }

        public void OnExited(int handle)
        {
        }
    }
}
