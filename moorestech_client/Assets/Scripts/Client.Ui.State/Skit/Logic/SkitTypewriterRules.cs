using System;

namespace Client.Ui.State.Skit.Logic
{
    /// <summary>
    /// タイプライタとクリックの規則（Web の features/skit/interaction.ts の写し）。字はコードポイントで数える（Array.from と同じ）
    /// Typewriter and click rules (a copy of Web features/skit/interaction.ts); characters are counted as code points (same as Array.from)
    /// </summary>
    public static class SkitTypewriterRules
    {
        // クリックの帰結は 1 か所で決め、挙動と送りの印の両方が同じ値を使う
        // The click outcome is decided in one place and both the behavior and the advance marker share it
        public static SkitClickOutcome ClickOutcome(int visibleCount, int bodyLength, bool advanceAllowed)
        {
            if (visibleCount < bodyLength) return SkitClickOutcome.Reveal;
            return advanceAllowed ? SkitClickOutcome.Advance : SkitClickOutcome.None;
        }

        // Web の setInterval と同じく、間隔 1 つごとに 1 字ずつ増える
        // Like the Web setInterval, one character is added per interval
        public static int RevealCountAt(float elapsedMilliseconds, int intervalMs, int totalCount)
        {
            return Math.Min(totalCount, (int)Math.Floor(elapsedMilliseconds / intervalMs));
        }

        // 復元の提示（View が載って最初の描画）・即時表示の方式・間隔 0 は打ち出さない
        // Restored snapshots (the first draw after mounting), the instant mode and a zero interval skip the typing
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

        // 先頭から codePoints 個のコードポイントが占める UTF-16 の長さ
        // UTF-16 length taken by the first codePoints code points
        public static int PrefixLengthOfCodePoints(string text, int codePoints)
        {
            var index = 0;
            for (var taken = 0; taken < codePoints && index < text.Length; taken++) index += char.IsSurrogatePair(text, index) ? 2 : 1;
            return index;
        }
    }
}
