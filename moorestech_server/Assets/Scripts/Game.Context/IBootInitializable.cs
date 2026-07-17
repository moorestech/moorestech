namespace Game.Context
{
    /// <summary>
    ///     サーバー起動時にDIコンテナから自動で一括生成されるサービスのマーカー。購読等の初期化はコンストラクタでなくLoadで行い、Loadは起動時に一括で呼ばれる
    ///     Marker for services auto-created in bulk by the DI container at server boot. Initialization such as subscriptions belongs in Load, not the constructor; Load is invoked in bulk at boot
    /// </summary>
    public interface IBootInitializable
    {
        void Load();
    }
}
