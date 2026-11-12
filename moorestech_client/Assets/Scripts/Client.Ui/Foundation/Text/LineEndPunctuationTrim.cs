using System.Collections.Generic;
using System.Text;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 行末の閉じ約物を Chrome の text-spacing-trim: normal と同じく半角に詰めて折り返すための文字列の整形
    /// String shaping that wraps with line-end closing punctuation trimmed to half width like Chrome's text-spacing-trim: normal
    /// </summary>
    /// <remarks>
    /// TextCore（standard）の折り返し判定は「字の送り位置＋字の送り幅 ≦ 行幅」で、字の位置を変えずに約物の送り幅だけを半分にする rich text が無い。
    /// そこで 2 段で行う: 測る文（ToBreakMeasureText）は約物の前で 0.5em 戻し後ろで 0.5em 進めるので、約物は半分入れば行に残り（Chrome の allow-end）行中の後続は動かない。
    /// その折り返し位置で原文に改行を入れた表示文（Apply）を自動の折り返し無しで描くと、約物の字は元の位置のまま行末で空白の半分だけ箱をはみ出す。
    /// TextCore (standard) breaks when "pen position + glyph advance > line width", and no rich text halves only the punctuation's advance while keeping its glyph in place.
    /// So it takes two steps: the measuring text (ToBreakMeasureText) steps back 0.5em before and forward 0.5em after each mark, so a mark stays on the line when half of it fits (Chrome's allow-end) and later glyphs on the line do not move.
    /// The display text (Apply) puts newlines into the source at those breaks and is drawn without automatic wrapping, so the glyph keeps its place and only its blank half overhangs the box at line end.
    /// </remarks>
    internal static class LineEndPunctuationTrim
    {
        private const string StepBackTag = "<space=-0.5em>";
        private const string StepForwardTag = "<space=0.5em>";

        // R-2 の閉じ約物（全角の閉じ括弧・句読点）。Chrome は行末でこれらの右半分の空きを詰める
        // The R-2 closing marks (fullwidth closing brackets and stops); Chrome trims their blank right half at line end
        public static bool IsClosingPunctuation(char c)
        {
            return c == '）' || c == '」' || c == '』' || c == '】' || c == '〕' || c == '》' || c == '〉' || c == '。' || c == '、';
        }

        public static bool ContainsClosingPunctuation(string source)
        {
            foreach (var c in source)
            {
                if (IsClosingPunctuation(c)) return true;
            }
            return false;
        }

        // 閉じ約物ごとに前で 0.5em 戻し後ろで 0.5em 進めた、折り返し位置を測るための文
        // Text for measuring breaks: each closing mark steps back 0.5em before and forward 0.5em after
        public static string ToBreakMeasureText(string source)
        {
            var builder = new StringBuilder(source.Length + 16);
            foreach (var c in source)
            {
                if (!IsClosingPunctuation(c))
                {
                    builder.Append(c);
                    continue;
                }
                builder.Append(StepBackTag).Append(c).Append(StepForwardTag);
            }
            return builder.ToString();
        }

        // 行末の字（描かれる字の通し番号。rich text の印は数えない）の直後に改行を入れる。既に改行の位置には足さない
        // Insert a newline after each line-end glyph (index among presented glyphs, rich-text tags not counted); skip ones that are already newlines
        public static string Apply(string source, IReadOnlyList<int> lineEndIndices)
        {
            var builder = new StringBuilder(source.Length + lineEndIndices.Count);
            var presentedIndex = 0;
            var nextLineEnd = 0;
            var insideTag = false;
            foreach (var c in source)
            {
                builder.Append(c);
                if (IsTagBoundary(c)) continue;
                if (insideTag) continue;

                if (nextLineEnd < lineEndIndices.Count && lineEndIndices[nextLineEnd] == presentedIndex)
                {
                    if (c != '\n') builder.Append('\n');
                    nextLineEnd++;
                }
                presentedIndex++;
            }
            return builder.ToString();

            #region Internal

            // rich text の印（<…>）の中は描かれる字に数えない
            // Characters inside a rich-text tag (<...>) are not presented glyphs
            bool IsTagBoundary(char c)
            {
                if (c == '<') { insideTag = true; return true; }
                if (c == '>' && insideTag) { insideTag = false; return true; }
                return false;
            }

            #endregion
        }
    }
}
