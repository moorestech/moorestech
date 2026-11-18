namespace Client.Ui.State.Skit
{
    /// <summary>
    /// 選択肢 1 件。文は Unity 側で解決済みの表示文字列（Web と同じく辞書を通さない）
    /// One choice; the label is a display string already resolved on the Unity side (bypasses the dictionary as on the Web)
    /// </summary>
    public sealed class SkitChoiceItem
    {
        public string ChoiceId { get; }
        public string Label { get; }

        public SkitChoiceItem(string choiceId, string label)
        {
            ChoiceId = choiceId;
            Label = label;
        }
    }
}
