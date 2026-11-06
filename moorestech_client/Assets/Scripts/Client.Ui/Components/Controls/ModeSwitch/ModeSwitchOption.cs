using Client.Ui.State.Text;

namespace Client.Ui.Components.Controls
{
    public enum ModeSwitchOrientation
    {
        Horizontal,
        Vertical,
    }

    // Web の role。パネル内のビュー切替（§8.22）はタブ、既定は択一の組
    // The Web role: in-panel view switching (§8.22) is a tablist, the default is an exclusive group
    public enum ModeSwitchRole
    {
        Group,
        Tablist,
    }

    /// <summary>
    /// 選択肢 1 つ。文は UiText（辞書のキー・コンテンツのキー・翻訳しない生の文）
    /// One option; its label is a UiText (dictionary key, content key, or verbatim untranslated text)
    /// </summary>
    public sealed class ModeSwitchOption<TValue>
    {
        public string Testid { get; }
        public TValue Value { get; }
        public UiText Label { get; }

        public ModeSwitchOption(string testid, TValue value, UiText label)
        {
            Testid = testid;
            Value = value;
            Label = label;
        }
    }
}
