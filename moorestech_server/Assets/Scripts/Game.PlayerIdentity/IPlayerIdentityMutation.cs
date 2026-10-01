namespace Game.PlayerIdentity
{
    // 対応表を書き換える面。下見の確定だけを持つ
    // The mutating face of the table; it only commits a preview
    public interface IPlayerIdentityMutation
    {
        // 下見結果を接続確定後に反映する
        // Commit the preview after the connection has been bound
        void Commit(PlayerIdAssignment previewed);
    }
}
