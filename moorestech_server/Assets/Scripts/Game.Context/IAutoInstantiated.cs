namespace Game.Context
{
    /// <summary>
    ///     DIコンテナ構築時に確実にインスタンスが自動生成されることだけを表す基底マーカー。
    ///     直接実装せず、初期化タイミングを表す IBootInitializable / IPostLoadInitializable のどちらかを実装すること（直接実装は起動時に検出されて失敗する）
    ///     Base marker that only expresses that an instance is reliably auto-created when the DI container is built.
    ///     Do not implement this directly — implement IBootInitializable or IPostLoadInitializable (direct implementations fail fast at boot)
    /// </summary>
    public interface IAutoInstantiated
    {
    }
}
