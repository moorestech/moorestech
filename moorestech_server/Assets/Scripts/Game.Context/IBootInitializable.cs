namespace Game.Context
{
    /// <summary>
    ///     サーバー起動時にDIコンテナから一括生成・初期化されるサービスのマーカー（VContainerのRegisterEntryPoint相当。コンストラクタで購読等の初期化を行う）
    ///     Marker for services created and initialized in bulk at server boot (RegisterEntryPoint equivalent; initialization such as subscriptions runs in constructors)
    /// </summary>
    public interface IBootInitializable
    {
    }
}
