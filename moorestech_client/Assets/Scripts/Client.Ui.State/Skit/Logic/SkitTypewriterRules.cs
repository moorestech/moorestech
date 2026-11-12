using System;

namespace Client.Ui.State.Skit.Logic
{
    /// <summary>
    /// タイプライタとクリックの規則（Web の features/skit/interaction.ts の写し）。字はコードポイントで数える（Array.from と同じ）
    /// Typewriter and click rules (a copy of Web features/skit/interaction.ts); characters are counted as code points (same as Array.from)
    /// </summary>
    public static class SkitTypewriterRules
    {
        // クリックの帰結を 1 か所で決める
        // Decide the click outcome in one place
        public static SkitClickOutcome ClickOutcome(int visibleCount, int bodyLength, bool advanceAllowed)
        {
            if (visibleCount < bodyLength) return SkitClickOutcome.Reveal;
            return advanceAllowed ? SkitClickOutcome.Advance : SkitClickOutcome.None;
        }

        // 間隔ごとに 1 字増える（Web setInterval 同様）
        // One character per interval (like Web setInterval)
        public static int RevealCountAt(float elapsedMilliseconds, int intervalMs, int totalCount)
        {
            return Math.Min(totalCount, (int)Math.Floor(elapsedMilliseconds / intervalMs));
        }

        // 復元・即時表示・間隔 0 は打ち出さない
        // Restore, instant mode and zero interval skip typing
        public static bool ShouldRevealImmediately(bool restoringSnapshot, bool revealsByTypewriter, int intervalMs)
        {
            return restoringSnapshot || !revealsByTypewriter || intervalMs <= 0;
        }

        public static int CountCodePoints(string text)
        {
            var count = 0;
            for (var index = 0; index < text.Length; index += char.IsSurrogatePair(text, index) ? 2 : 1) count++;
            return count;
        }

        // 先頭 codePoints 個分の UTF-16 長
        // UTF-16 length of the first codePoints
        public static int PrefixLengthOfCodePoints(string text, int codePoints)
        {
            var index = 0;
            for (var taken = 0; taken < codePoints && index < text.Length; taken++) index += char.IsSurrogatePair(text, index) ? 2 : 1;
            return index;
        }
    }
}
