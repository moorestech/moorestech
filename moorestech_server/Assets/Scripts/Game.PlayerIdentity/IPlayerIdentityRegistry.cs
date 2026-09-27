namespace Game.PlayerIdentity
{
    public interface IPlayerIdentityRegistry
    {
        bool TryGetPlayerId(string identity, out int playerId);

        // 接続前の候補確認。身元・候補・次IDは変更しない
        // Preview before binding without changing identities, the candidate or the next id
        PlayerIdAssignment PreviewAssignment(string identity);

        // 既知ならそのID、未知なら候補か新規IDを払い出す
        // Returns the known id, or claims the candidate / issues a new id for an unknown identity
        PlayerIdAssignment Assign(string identity);
    }
}
