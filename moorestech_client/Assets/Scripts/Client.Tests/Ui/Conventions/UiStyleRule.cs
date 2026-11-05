namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// GameUi の USS・UXML の規約（plan §3.5）の 1 項目
    /// One rule of the GameUi USS/UXML conventions (plan §3.5)
    /// </summary>
    public enum UiStyleRule
    {
        // トークン USS の外での色リテラル（#・rgb(・rgba(）
        // A color literal (#, rgb(, rgba() outside the token USS
        ColorLiteral,
        // :first-child・:last-child・:nth-child（並びに依存する見た目）
        // :first-child, :last-child, :nth-child (looks that depend on sibling order)
        StructuralPseudoClass,
        // 前後は層で決めるので z-index は使わない
        // Stacking is decided by layers, so z-index is never used
        ZIndex,
        // .png・.jpg・.psd の直接参照（画像は資産のカタログ経由）
        // Direct .png/.jpg/.psd references (images go through the asset catalog)
        BitmapReference,
        // -unity-font-style: bold（太字はフォントの重みで出す）
        // -unity-font-style: bold (weight comes from the font itself)
        BoldFontStyle,
        // UXML の text="…" に書いた文言（文言は翻訳経由で入れる）
        // Wording written into a UXML text="…" (wording goes through localization)
        UxmlTextLiteral,
        // 同じ UXML の中で name が重複（testid と Q(name) が曖昧になる）
        // A name repeated within one UXML (makes the testid and Q(name) ambiguous)
        DuplicateName,
    }

    /// <summary>
    /// 規約違反 1 件。行番号は 1 始まり
    /// One convention violation; the line number is 1-based
    /// </summary>
    public readonly struct UiStyleViolation
    {
        public readonly UiStyleRule Rule;
        public readonly int Line;
        public readonly string Excerpt;

        public UiStyleViolation(UiStyleRule rule, int line, string excerpt)
        {
            Rule = rule;
            Line = line;
            Excerpt = excerpt;
        }

        public override string ToString()
        {
            return $"{Rule} at line {Line}: {Excerpt}";
        }
    }
}
