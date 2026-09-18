using System;

namespace Game.Paths
{
    // worldDefinition と契約値の文字列の変換を1箇所に閉じる。書き側・再現側・取り込みスクリプトと共有する綴りの正本（PlaytestReportKindText と同じ形）
    // Keeps the conversion between worldDefinition and its contract string in one place; the source of truth for the spelling shared by the writer, reproducer and ingest scripts (same shape as PlaytestReportKindText)
    public static class BugReportWorldDefinitionText
    {
        private const string NotCapturedText = "not-captured";
        private const string FullText = "full";
        private const string GeneratedWorldJsonOnlyText = "generated-world-json-only";

        public static string ToContractText(BugReportWorldDefinition definition)
        {
            return definition switch
            {
                BugReportWorldDefinition.NotCaptured => NotCapturedText,
                BugReportWorldDefinition.Full => FullText,
                BugReportWorldDefinition.GeneratedWorldJsonOnly => GeneratedWorldJsonOnlyText,
                _ => throw new ArgumentOutOfRangeException(nameof(definition), definition, "契約値の無い worldDefinition"),
            };
        }

        // 綴りの完全一致だけを受け付ける。大文字違い・数値・未知の語は false
        // Only the exact spelling is accepted; a case variant, a number or an unknown word yields false
        public static bool TryParse(string text, out BugReportWorldDefinition definition)
        {
            switch (text)
            {
                case NotCapturedText:
                    definition = BugReportWorldDefinition.NotCaptured;
                    return true;
                case FullText:
                    definition = BugReportWorldDefinition.Full;
                    return true;
                case GeneratedWorldJsonOnlyText:
                    definition = BugReportWorldDefinition.GeneratedWorldJsonOnly;
                    return true;
                default:
                    definition = BugReportWorldDefinition.NotCaptured;
                    return false;
            }
        }
    }
}
