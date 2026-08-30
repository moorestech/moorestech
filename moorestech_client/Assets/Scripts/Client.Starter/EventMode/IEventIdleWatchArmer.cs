namespace Client.Starter.EventMode
{
    /// <summary>
    /// 無操作監視を武装する窓口。武装の順序をテストから観測するための境界。
    /// The window that arms idle watching; the seam that lets tests observe the arming order.
    /// </summary>
    public interface IEventIdleWatchArmer
    {
        void ArmIdleWatch(int idleTimeoutSeconds);
    }

    /// <summary>
    /// 実機の武装。監視オブジェクトの生成そのものが武装になる。
    /// The production arming: creating the watcher object is the arming itself.
    /// </summary>
    public class EventIdleQuitWatcherArmer : IEventIdleWatchArmer
    {
        public void ArmIdleWatch(int idleTimeoutSeconds)
        {
            EventIdleQuitWatcher.Create(idleTimeoutSeconds);
        }
    }
}
