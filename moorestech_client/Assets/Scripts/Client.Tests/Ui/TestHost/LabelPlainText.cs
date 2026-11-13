using System.Text.RegularExpressions;
using Client.Ui.Foundation.Text.WrapSafe;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// ラベルの文からリッチテキストの印を外して読む
    /// Reads a label's text without its rich-text tags
    /// </summary>
    /// <remarks>
    /// WrapSafeLabel の派生は表示用の印や改行を前置・挿入しうるので、原文（WordsText）を読む。どちらも印は外す
    /// WrapSafeLabel subclasses may prefix display tags or insert newlines, so their source (WordsText) is read; tags are dropped either way
    /// </remarks>
    public static class LabelPlainText
    {
        private static readonly Regex RichTextTag = new("<[^>]+>");

        public static string PlainText(this Label label)
        {
            var text = label is WrapSafeLabel wrapSafe ? wrapSafe.WordsText : label.text;
            return RichTextTag.Replace(text, string.Empty);
        }
    }
}
