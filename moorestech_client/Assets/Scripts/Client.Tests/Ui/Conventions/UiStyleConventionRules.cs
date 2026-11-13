using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// USS・UXML をテキストとして読み、GameUi の規約違反を行番号付きで返す検査関数（L2）
    /// Reads USS/UXML as text and returns GameUi convention violations with line numbers (L2)
    /// </summary>
    /// <remarks>
    /// 拡張点: 規約を足すときは UiStyleRule に項目を足し、ここに検査を 1 つ足し、UiStyleConventionTest に違反を含む文字列のケースを 1 つ足す
    /// Extension: to add a convention, add an item to UiStyleRule, one check here, and one violating-string case to UiStyleConventionTest
    /// </remarks>
    public static class UiStyleConventionRules
    {
        // 色の定義元なので色リテラルだけを許すトークン USS
        // Token USS files that define the colors, so only color literals are allowed in them
        public static readonly string[] ColorLiteralExemptUssFileNames = { "Tokens.uss", "SkitTokens.uss", "SlotTokens.uss", "MantineTokens.uss", "PanelsDerivedTokens.uss", "ControlsDerivedTokens.uss", "TextDerivedTokens.uss", "SlotsDerivedTokens.uss", "GaugesDerivedTokens.uss", "RecipeDerivedTokens.uss", "ScreensDerivedTokens.uss", "SkitDerivedTokens.uss" };

        private static readonly Regex UssComment = new(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex UxmlComment = new(@"<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex DeclarationBlock = new(@"\{([^{}]*)\}");
        private static readonly Regex Declaration = new(@"([-\w]+)\s*:\s*([^;]*)");
        private static readonly Regex UrlValue = new(@"url\([^)]*\)", RegexOptions.IgnoreCase);
        private static readonly Regex HexColor = new(@"#[0-9a-fA-F]{3,8}\b");
        private static readonly Regex RgbFunction = new(@"\brgba?\s*\(", RegexOptions.IgnoreCase);
        private static readonly Regex StructuralPseudoClass = new(@":(?:first-child|last-child|nth-child|nth-last-child)\b");
        private static readonly Regex BitmapReference = new(@"\.(?:png|jpe?g|psd)\b", RegexOptions.IgnoreCase);
        private static readonly Regex UxmlText = new(@"(?<![\w:-])text\s*=\s*""([^""]*)""");
        private static readonly Regex UxmlName = new(@"(?<![\w:-])name\s*=\s*""([^""]+)""");
        private static readonly Regex UxmlStyle = new(@"(?<![\w:-])style\s*=\s*""([^""]*)""");

        public static List<UiStyleViolation> CheckUss(string ussText, bool allowColorLiterals)
        {
            var text = BlankOut(ussText, UssComment);
            var violations = new List<UiStyleViolation>();
            AddMatches(violations, text, StructuralPseudoClass, UiStyleRule.StructuralPseudoClass);
            AddMatches(violations, text, BitmapReference, UiStyleRule.BitmapReference);

            // 色・z-index・太字は宣言の中だけを見る（#name の ID セレクタを色と取り違えない）
            // Colors, z-index and bold are checked only inside declarations, so a #name id selector is never taken for a color
            foreach (Match block in DeclarationBlock.Matches(text))
            {
                CheckDeclarations(violations, text, block.Groups[1].Value, block.Groups[1].Index, allowColorLiterals);
            }
            return violations;
        }

        public static List<UiStyleViolation> CheckUxml(string uxmlText)
        {
            var text = BlankOut(uxmlText, UxmlComment);
            var violations = new List<UiStyleViolation>();
            AddMatches(violations, text, BitmapReference, UiStyleRule.BitmapReference);

            // 空の text="" は文言を持たないので許す
            // An empty text="" carries no wording, so it is allowed
            foreach (Match match in UxmlText.Matches(text))
            {
                if (0 < match.Groups[1].Value.Length) violations.Add(Violation(text, match.Index, UiStyleRule.UxmlTextLiteral, match.Value));
            }

            // 2 回目以降の出現を違反にする
            // Every occurrence after the first is a violation
            var seenNames = new HashSet<string>();
            foreach (Match match in UxmlName.Matches(text))
            {
                if (!seenNames.Add(match.Groups[1].Value)) violations.Add(Violation(text, match.Index, UiStyleRule.DuplicateName, match.Value));
            }

            // インライン style は USS の宣言と同じ規則で見る（UXML に色の例外は無い）
            // Inline styles follow the same rules as USS declarations (UXML has no color exemption)
            foreach (Match match in UxmlStyle.Matches(text))
            {
                CheckDeclarations(violations, text, match.Groups[1].Value, match.Groups[1].Index, false);
            }
            return violations;
        }

        private static void CheckDeclarations(List<UiStyleViolation> violations, string fullText, string declarations, int offset, bool allowColorLiterals)
        {
            foreach (Match declaration in Declaration.Matches(declarations))
            {
                var property = declaration.Groups[1].Value;
                var value = UrlValue.Replace(declaration.Groups[2].Value, "url()");
                var index = offset + declaration.Index;
                if (property == "z-index") violations.Add(Violation(fullText, index, UiStyleRule.ZIndex, declaration.Value));
                if (property == "-unity-font-style" && value.TrimStart().StartsWith("bold")) violations.Add(Violation(fullText, index, UiStyleRule.BoldFontStyle, declaration.Value));
                if (!allowColorLiterals && (HexColor.IsMatch(value) || RgbFunction.IsMatch(value))) violations.Add(Violation(fullText, index, UiStyleRule.ColorLiteral, declaration.Value));
            }
        }

        private static void AddMatches(List<UiStyleViolation> violations, string text, Regex pattern, UiStyleRule rule)
        {
            foreach (Match match in pattern.Matches(text))
            {
                violations.Add(Violation(text, match.Index, rule, match.Value));
            }
        }

        // コメントは同じ長さの空白に置き換え、行番号をずらさない
        // Replace comments with same-length whitespace so line numbers stay put
        private static string BlankOut(string text, Regex comment)
        {
            return comment.Replace(text, match => Regex.Replace(match.Value, @"[^\n]", " "));
        }

        private static UiStyleViolation Violation(string text, int index, UiStyleRule rule, string excerpt)
        {
            var line = 1;
            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n') line++;
            }
            return new UiStyleViolation(rule, line, excerpt.Trim());
        }
    }
}
