// S/Client.Ui/Screens/Skit/Parts/Window/SkitRichText.cs
using Client.Ui.State.Skit.Logic;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// スキットの文を Label のリッチテキストへ組む。文の中の山括弧はタグにせず、字を 1 つも削らずに字のまま出す（Web は文字列をそのまま描く）
    /// Builds skit text into Label rich text; angle brackets inside the text stay literal and no character is dropped (the Web draws the string as is)
    /// </summary>
    public static class SkitRichText
    {
        // 残りの字を透明にする開きタグ。Task 8 Step 1 の実測で決める（D-10）
        // Opening tag that turns the remaining characters transparent; settled by the Task 8 Step 1 measurement (D-10)
        public const string HiddenTailOpenTag = "<alpha=#00>";
        // 「<」の直後に挟む幅 0 の字。語結合子は改行の機会も作らない。Task 8 Step 1 の実測で決める（D-48）
        // Zero-width character inserted right after '<'; a word joiner adds no line-break opportunity; settled by the Task 8 Step 1 measurement (D-48)
        private const string TagBreaker = "\u2060";
        private const string BackgroundSeparator = " : ";

        public static string Literal(string text)
        {
            // どの「<」もタグの始まりにならないよう、直後に幅 0 の字を挟む。noparse と違い閉じタグの字面も削らずに済む
            // Insert a zero-width character after every '<' so none starts a tag; unlike noparse, a literal closing tag needs no removal
            return text.Replace("<", "<" + TagBreaker);
        }

        // 出した字の後ろに残りの字を透明で続け、折り返しと行の位置を全文と同じに保つ（A-82）
        // Follow the shown characters with the rest in transparent, keeping wrapping and line positions identical to the full text (A-82)
        public static string TypewriterBody(string body, int visibleCodePoints)
        {
            var shownLength = SkitTypewriterRules.PrefixLengthOfCodePoints(body, visibleCodePoints);
            if (shownLength >= body.Length) return Literal(body);
            return Literal(body.Substring(0, shownLength)) + HiddenTailOpenTag + Literal(body.Substring(shownLength));
        }

        // 背景の 1 行は Web と同じく切り出す。中央寄せの 1 行なので透明な残りを足すと位置が Web とずれる（D-9）
        // The background line slices like the Web; it is a centered single line, so a transparent tail would shift it from the Web (D-9)
        public static string BackgroundLine(string speaker, string body, int visibleCodePoints)
        {
            var visibleBody = body.Substring(0, SkitTypewriterRules.PrefixLengthOfCodePoints(body, visibleCodePoints));
            return Literal(speaker.Length == 0 ? visibleBody : speaker + BackgroundSeparator + visibleBody);
        }
    }
}
