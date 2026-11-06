// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintSplitKind.cs
namespace Client.Ui.Components.Text
{
    /// <summary>
    /// 解いた文をどう割ったか。印の欠けを別の種類にし、-1 と bool の組で 3 状態を表さない（D-19）
    /// How a resolved sentence was split; a missing marker is its own kind instead of a -1 plus bool pair (D-19)
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
