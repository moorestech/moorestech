// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintSegments.cs
using System;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// 解いた文を kbd の前と後ろへ割った結果（Web の LocalizedShortcutHint の markerIndex の分岐を種類で表す）
    /// A resolved sentence split into the parts before and after the kbd (the markerIndex branch of Web LocalizedShortcutHint, as a kind)
    /// </summary>
    public readonly struct ShortcutHintSegments
    {
        // Web の SHORTCUT_MARKER と引数名。翻訳文の {shortcut} にこの印を入れて位置を探す
        // Web SHORTCUT_MARKER and param name; this marker fills {shortcut} in the sentence to locate the kbd
        public const string Marker = "\uE000";
        public const string ParamName = "shortcut";

        public readonly ShortcutHintSplitKind Kind;
        public readonly string Before;
        public readonly string After;

        private ShortcutHintSegments(ShortcutHintSplitKind kind, string before, string after)
        {
            Kind = kind;
            Before = before;
            After = after;
        }

        public static ShortcutHintSegments Split(string resolved, ShortcutHintLayout layout)
        {
            // prefix は割らず kbd を先頭にする
            // Prefix never splits; the kbd goes first
            if (layout == ShortcutHintLayout.Prefix) return new ShortcutHintSegments(ShortcutHintSplitKind.KeyFirst, "", resolved);

            // inline は印の位置で割る。印が無いのは辞書の破損なので kbd を先頭へ退避し、その種類で返す
            // Inline splits at the marker; a missing marker means a broken dictionary, so fall back to key-first and say so
            var markerIndex = resolved.IndexOf(Marker, StringComparison.Ordinal);
            if (markerIndex < 0) return new ShortcutHintSegments(ShortcutHintSplitKind.KeyFirstBecauseMarkerMissing, "", resolved);
            return new ShortcutHintSegments(ShortcutHintSplitKind.Inline, resolved.Substring(0, markerIndex), resolved.Substring(markerIndex + Marker.Length));
        }
    }
}
