namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     ブループリントでコピー可能な「設定」を提供するコンポーネント
    ///     実行時状態（インベントリ・加工進捗）は含めないこと
    ///     Provides copyable "settings" for blueprints; exclude runtime state
    /// </summary>
    public interface IBlockBlueprintSettings : IBlockComponent
    {
        string BlueprintSettingsKey { get; }

        // 可読JSON。アイテム等の参照はGUID文字列で表現する
        // Readable JSON; represent item references as GUID strings
        string GetBlueprintSettingsJson();

        // 抽出と対の適用側。BlockFactory.Create直後に呼ぶ
        // Apply counterpart; invoked by BlockFactory.Create right after creation
        void ApplyBlueprintSettingsJson(string json);
    }
}
