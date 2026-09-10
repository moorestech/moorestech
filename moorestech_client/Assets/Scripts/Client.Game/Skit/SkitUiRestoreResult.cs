namespace Client.Game.Skit
{
    // 会話UI復帰要求の帰結。「隠れていない」と「storeに拒否された」を呼び出し元が区別できるようにする
    // Outcome of a dialogue-UI restore request, letting the caller tell "nothing was hidden" from "the store refused"
    public enum SkitUiRestoreResult
    {
        NothingHidden,
        Restored,
        Rejected,
    }
}
