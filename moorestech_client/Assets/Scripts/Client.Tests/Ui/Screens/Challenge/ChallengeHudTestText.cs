using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Tests.Ui.TestHost;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge
{
    /// <summary>
    /// チャレンジ HUD のテスト用に、目標の文から行高のリッチテキストの印を外して取り出し、解決済みの文字の影を読む
    /// Test helper that reads the challenge HUD objective texts without the line-height rich-text tags and reads the resolved text shadow
    /// </summary>
    internal static class ChallengeHudTestText
    {
        private static readonly FieldInfo ComputedStyleField = typeof(VisualElement).GetField("m_Style", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo TextShadowProperty = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.ComputedStyle").GetProperty("textShadow");

        public static List<string> Objectives(VisualElement hud)
        {
            return hud.Query<Label>("challenge-objective").ToList().Select(label => label.PlainText()).ToList();
        }

        // resolvedStyle に textShadow が無い（Unity 6.3）ので、解決済みの計算スタイルを反射で読む
        // resolvedStyle has no textShadow (Unity 6.3), so the resolved computed style is read via reflection
        public static TextShadow ResolvedTextShadow(VisualElement element)
        {
            return (TextShadow)TextShadowProperty.GetValue(ComputedStyleField.GetValue(element));
        }
    }
}
