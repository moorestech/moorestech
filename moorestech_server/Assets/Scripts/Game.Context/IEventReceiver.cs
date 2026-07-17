namespace Game.Context
{
    /// <summary>
    ///     サーバー起動直後に一括生成されるイベントレシーバーのマーカー。コンストラクタで購読を開始する
    ///     Marker for event receivers materialized right after server boot; they subscribe in their constructors
    /// </summary>
    public interface IEventReceiver
    {
    }
}
