using UnityEngine;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 文を折り返しても UITK TextCore が固まらない最小の内容の幅を求める
    /// Computes the narrowest content width at which UITK TextCore can wrap a text without freezing
    /// </summary>
    /// <remarks>
    /// Unity 6000.3.8f1 の TextCore は「字＋行末に置けない字（開き括弧等）の連なり＋字」を、連なりと次の字が収まらない幅で折り返すと配置でも描画でも戻らない（bd moorestech-fcpzz）。
    /// 実測（15px）: 「ん（前」は幅 29 で固まり 31 で通る、「ん（「前」は 31 で固まり 46 で通る、字間 5.5 の「ん（前」は 33 で通る。文頭の「（前」、空白、閉じ約物だけの文は幅 0〜20 でも通る。
    /// よって必要な幅は（連なりの字数＋1）×（字の大きさ＋字間）＋1px とする（字間を全部の字に足すので実測より広めに倒す）。設計書 A-224
    /// TextCore in Unity 6000.3.8f1 never returns from layout or rendering when it wraps "glyph + run of no-break-after glyphs (opening brackets etc.) + glyph" at a width that cannot hold the run plus the next glyph (bd moorestech-fcpzz).
    /// Measured at 15px: "ん（前" freezes at 29 and passes at 31, "ん（「前" freezes at 31 and passes at 46, "ん（前" with 5.5 spacing passes at 33; a leading "（前", spaces and closing-only texts pass at 0-20.
    /// The required width is therefore (run length + 1) x (font size + letter spacing) + 1px, erring wide by spacing every glyph. Design A-224
    /// </remarks>
    public static class NarrowWrapWidth
    {
        // TextCore の既定の禁則（PanelTextSettings の leadingCharacters＝行末に置けない字）から空白を除いた写し。NarrowWrapWidthTest が既定の設定と照合する
        // Copy of TextCore's default no-break-after set (PanelTextSettings leadingCharacters) minus the space; NarrowWrapWidthTest checks it against the default settings
        public const string NoBreakAfterCharacters = "([｛〔〈《「『【〘〖〝‘“｟«$—…‥〳〴〵\\［（{£¥\"々〇＄￥￦#";
        private const float SafetyMarginPixels = 1f;

        // 固まる並びが無ければ 0
        // 0 when the text has no freezing sequence
        public static float Of(string text, bool richText, float fontSize, float letterSpacing)
        {
            var longestRun = LongestRunBetweenGlyphs(text, richText);
            if (longestRun == 0) return 0f;
            return (longestRun + 1) * (fontSize + Mathf.Max(0f, letterSpacing)) + SafetyMarginPixels;
        }

        // 前に字があり、後ろにも字が続く「行末に置けない字」の連なりのうち最長の字数（リッチテキストの印は字に数えない）
        // Longest run of no-break-after glyphs that has a glyph before it and one after it (rich text tags are not glyphs)
        public static int LongestRunBetweenGlyphs(string text, bool richText)
        {
            var longest = 0;
            var run = 0;
            var hasGlyphBeforeRun = false;
            for (var index = 0; index < text.Length; index++)
            {
                var c = text[index];
                if (richText && c == '<')
                {
                    // silent-ok: 閉じない < は字として読む / 印でなければ TextCore も字として描く
                    var tagEnd = text.IndexOf('>', index);
                    if (tagEnd > index)
                    {
                        index = tagEnd;
                        continue;
                    }
                }

                // 改行は行頭に戻すので、その後の連なりは前に字が無い扱い
                // A newline returns to the line start, so the following run has no glyph before it
                if (c == '\n')
                {
                    run = 0;
                    hasGlyphBeforeRun = false;
                    continue;
                }
                if (NoBreakAfterCharacters.IndexOf(c) >= 0)
                {
                    run++;
                    continue;
                }
                if (run > 0 && hasGlyphBeforeRun) longest = Mathf.Max(longest, run);
                run = 0;
                hasGlyphBeforeRun = true;
            }
            return longest;
        }
    }
}
