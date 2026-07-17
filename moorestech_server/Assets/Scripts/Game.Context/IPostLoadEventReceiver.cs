namespace Game.Context
{
    /// <summary>
    ///     初期ロード完了後に一括生成されるイベントレシーバーのマーカー。ロード中のイベントをクライアントへ配信しないために生成を遅らせる
    ///     Marker for event receivers materialized after initial world load; creation is deferred so load-time events are not broadcast to clients
    /// </summary>
    public interface IPostLoadEventReceiver
    {
    }
}
