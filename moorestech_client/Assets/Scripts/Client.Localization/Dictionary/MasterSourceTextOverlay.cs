using System.Collections.Generic;

namespace Client.Localization
{
    internal static class MasterSourceTextOverlay
    {
        public static void Apply(
            LocalizationDictionaryCandidate candidate,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            foreach (var sourceText in masterSourceTexts)
            {
                // 空Masterはmod由来Sourceを残さずcanonical欠落にする
                // Empty Master removes mod Source so the canonical value remains missing
                if (string.IsNullOrEmpty(sourceText.Value))
                {
                    candidate.SourceTexts.Remove(sourceText.Key);
                    continue;
                }

                candidate.SourceTexts[sourceText.Key] = sourceText.Value;
            }
        }
    }
}
