// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintSplitKind.cs
namespace Client.Ui.Components.Text
{
    /// <summary>
    /// 文の割り方。印の欠けは別の種類
    /// How a sentence was split; missing marker is its own kind
    /// </summary>
    public enum ShortcutHintSplitKind
    {
        // prefix: 割らず kbd を先頭に置く
        // Prefix: no split, the kbd goes first
        KeyFirst,

        // inline: 印の位置で前後に割った
        // Inline: split at the marker
        Inline,

        // inline なのに印が無い（辞書の破損）。kbd を先頭へ退避した
        // Inline but the marker is missing (broken dictionary); the kbd was moved first
        KeyFirstBecauseMarkerMissing,
    }
}
