namespace Game.PlayerIdentity
{
    // 対応表を変更しない読み取り面。接続前の下見と登録済み判定だけを持つ
    // The read-only face of the table: previewing before binding and asking whether an id is registered
    public interface IPlayerIdentityLookup
    {
        // 接続前の候補確認。身元・候補・次IDは変更しない
        // Preview before binding without changing identities, the candidate or the next id
        PlayerIdAssignment PreviewAssignment(string identity);

        bool IsRegisteredPlayerId(long playerId);
    }
}
