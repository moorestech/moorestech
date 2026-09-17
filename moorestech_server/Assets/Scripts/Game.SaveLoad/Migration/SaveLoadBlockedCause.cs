namespace Game.SaveLoad.Migration
{
    /// <summary>セーブをロードせず中断した原因。プレイヤーの取るべき行動が原因ごとに違うため文言でなく型で区別する</summary>
    /// <summary>Why a save was not loaded; the player's remedy differs per cause, so it is told apart by type rather than by text</summary>
    public enum SaveLoadBlockedCause
    {
        UnreadableJson,
        UnreadableWorldVersion,
        FutureVersion,
        InvalidVersion,
        StepFailed,
    }
}
