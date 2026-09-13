namespace Game.SaveLoad.Writer
{
    // 書き出しの用途。完了通知はこの種別ごとに分かれたキューで返る
    // The purpose of a write; completions return on a queue per kind
    public enum SaveWriteKind
    {
        PlayerSave = 0,
        Snapshot = 1,
    }
}
