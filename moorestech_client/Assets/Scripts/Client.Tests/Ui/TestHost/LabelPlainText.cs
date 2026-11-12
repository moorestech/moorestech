using System.Text.RegularExpressions;
using Client.Ui.Foundation.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// ラベルの文からリッチテキストの印を外して読む
    /// Reads a label's text without its rich-text tags
    /// </summary>
    /// <remarks>
    /// LocalizedLabel は行の高さを &lt;line-height=Npx&gt; で前置し、行末の約物を詰めるときは改行を入れるので、原文（SourceText）を読む。どちらも印は外す
    /// LocalizedLabel prefixes the line height as &lt;line-height=Npx&gt; and inserts newlines when trimming line-end punctuation, so its source (SourceText) is read; tags are dropped either way
    /// </remarks>
    public static class LabelPlainText
    {
        private static readonly Regex RichTextTag = new("<[^>]+>");

        public static string PlainText(this Label label)
        {
            var text = label is LocalizedLabel { SourceText: not null } localized ? localized.SourceText : label.text;
            return RichTextTag.Replace(text, string.Empty);
        }
    }
}
