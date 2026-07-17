namespace Game.Context
{
    /// <summary>
    ///     DIコンテナ構築時に確実にインスタンスが自動生成されるサービスの基底マーカー。購読等の初期化はコンストラクタでなくLoadで行い、Loadは子interfaceが示すタイミングで一括で呼ばれる。
    ///     直接実装せず、タイミングを表す IBootInitializable / IPostLoadInitializable のどちらかを実装すること（直接実装は起動時に検出されて失敗する）
    ///     Base marker for services whose instances are reliably auto-created when the DI container is built. Initialization such as subscriptions belongs in Load, not the constructor; Load is invoked in bulk at the timing indicated by the child interface.
    ///     Do not implement this directly — implement IBootInitializable or IPostLoadInitializable (direct implementations fail fast at boot)
    /// </summary>
    public interface IAutoInstantiated
    {
        void Load();
    }
}
