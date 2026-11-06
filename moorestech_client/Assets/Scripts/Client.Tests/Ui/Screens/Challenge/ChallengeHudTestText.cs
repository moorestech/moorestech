using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge
{
    /// <summary>
    /// チャレンジ HUD のテスト用に、目標の文から行高のリッチテキストの印を外して取り出し、解決済みの文字の影を読む
    /// Test helper that reads the challenge HUD objective texts without the line-height rich-text tags and reads the resolved text shadow
    /// </summary>
    internal static class ChallengeHudTestText
    {
        private static readonly Regex RichTextTag = new("<[^>]+>");
        private static readonly FieldInfo ComputedStyleField = typeof(VisualElement).GetField("m_Style", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo TextShadowProperty = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.ComputedStyle").GetProperty("textShadow");

        // LocalizedLabel は --localized-label-line-height を <line-height=Npx> で前置するので外す
        // LocalizedLabel prefixes --localized-label-line-height as <line-height=Npx>, so strip it
        public static string Plain(Label label)
        {
            return RichTextTag.Replace(label.text, string.Empty);
        }

        public static List<string> Objectives(VisualElement hud)
        {
            return hud.Query<Label>("challenge-objective").ToList().Select(Plain).ToList();
        }

        // resolvedStyle に textShadow が無い（Unity 6.3）ので、解決済みの計算スタイルを反射で読む
        // resolvedStyle has no textShadow (Unity 6.3), so the resolved computed style is read via reflection
        public static TextShadow ResolvedTextShadow(VisualElement element)
        {
            return (TextShadow)TextShadowProperty.GetValue(ComputedStyleField.GetValue(element));
        }
    }
}
