namespace Game.PlayerIdentity
{
    public interface IPlayerIdentityRegistry
    {
        // 接続前の候補確認。身元・候補・次IDは変更しない
        // Preview before binding without changing identities, the candidate or the next id
        PlayerIdAssignment PreviewAssignment(string identity);

        // 下見結果を接続確定後に反映する
        // Commit the preview after the connection has been bound
        void Commit(PlayerIdAssignment previewed);

        bool IsRegisteredPlayerId(int playerId);
    }
}
