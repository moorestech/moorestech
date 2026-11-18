namespace Client.Ui.Components.Decor
{
    /// <summary>
    /// 両端フェードの水平罫線。面の有無を問わず情報の階層を区切る（Web の shared/ui/FadeRule・装飾語彙 1）
    /// Horizontal rule fading at both ends that separates information levels with or without a face (Web shared/ui/FadeRule, decoration vocabulary 1)
    /// </summary>
    public sealed class FadeRule : FadeGradientRule
    {
        public FadeRule() : base("--fade-rule-start", "--fade-rule-peak", "--fade-rule-end")
        {
            AddToClassList("fade-rule");
        }
    }
}
