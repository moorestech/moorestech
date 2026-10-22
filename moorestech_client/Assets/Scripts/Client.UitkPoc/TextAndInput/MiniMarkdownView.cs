using System;
using System.Text.RegularExpressions;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Client.UitkPoc.TextAndInput
{
    // マークダウンの小さな部分集合をVisualElementの塊へ変換する。Webならmarked等で1行の処理
    // Converts a small markdown subset into VisualElement blocks; on the web a library call does this
    public class MiniMarkdownView : VisualElement
    {
        private static readonly Regex Bold = new(@"\*\*(.+?)\*\*");
        private static readonly Regex Code = new(@"`(.+?)`");
        private static readonly Regex Link = new(@"\[(.+?)\]\((.+?)\)");
        private static readonly Regex ItemIcon = new(@":item\[(.+?)\]:");

        private readonly Subject<(string linkId, Vector2 position)> _linkClicked = new();
        public IObservable<(string linkId, Vector2 position)> LinkClicked => _linkClicked;

        public MiniMarkdownView(string markdown)
        {
            AddToClassList("md-root");
            var lines = markdown.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (line.Length == 0) continue;

                // コードブロックは閉じ記号まで生の文字として1つの箱に入れる
                // A fenced code block goes verbatim into one box until the closing fence
                if (line.StartsWith("```"))
                {
                    var code = "";
                    for (i++; i < lines.Length && !lines[i].StartsWith("```"); i++) code += (code.Length > 0 ? "\n" : "") + lines[i];
                    Add(Block("md-code", code, false));
                    continue;
                }
                if (line == "---") Add(Block("md-rule", "", false));
                else if (line.StartsWith("## ")) Add(Block("md-h2", line.Substring(3), true));
                else if (line.StartsWith("# ")) Add(Block("md-h1", line.Substring(2), true));
                else if (line.StartsWith("> ")) Add(Block("md-quote", line.Substring(2), true));
                else if (line.StartsWith("- ")) Add(ListItem("・", line.Substring(2)));
                else if (Regex.IsMatch(line, @"^\d+\. ")) Add(ListItem(line.Substring(0, line.IndexOf(' ')), line.Substring(line.IndexOf(' ') + 1)));
                else Add(Block("md-p", line, true));
            }
        }

        private VisualElement ListItem(string marker, string text)
        {
            // 行頭記号と本文を横に並べ、本文側だけ折り返す（CSSのlist-style相当は無い）
            // Marker and body side by side with only the body wrapping (no list-style equivalent)
            var row = new VisualElement();
            row.AddToClassList("md-li");
            var markerLabel = new Label(marker);
            markerLabel.AddToClassList("md-li__marker");
            row.Add(markerLabel);
            row.Add(Block("md-li__body", text, true));
            return row;
        }

        private Label Block(string className, string text, bool inline)
        {
            var label = new Label(inline ? ToRichText(text) : text) { enableRichText = inline };
            label.AddToClassList(className);
            label.selection.isSelectable = true;
            label.RegisterCallback<PointerUpLinkTagEvent>(evt => _linkClicked.OnNext((evt.linkID, evt.position)));
            label.RegisterCallback<PointerOverLinkTagEvent>(_ => label.AddToClassList("md-link-hover"));
            label.RegisterCallback<PointerOutLinkTagEvent>(_ => label.RemoveFromClassList("md-link-hover"));
            return label;
        }

        private static string ToRichText(string text)
        {
            // 行内記法をTextCoreのリッチテキストタグへ置き換える
            // Replace inline syntax with TextCore rich-text tags
            text = ItemIcon.Replace(text, m => $"<sprite name=\"{m.Groups[1].Value}\">");
            text = Bold.Replace(text, "<b>$1</b>");
            text = Code.Replace(text, "<mark=#ffffff1a><color=#9cdcfe> $1 </color></mark>");
            return Link.Replace(text, "<link=\"$2\"><color=#6cb6ff><u>$1</u></color></link>");
        }
    }
}
