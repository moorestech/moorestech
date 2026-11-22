using System.Collections.Generic;
using System.Linq;
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
        public static List<string> Objectives(VisualElement hud)
        {
            return hud.Query<Label>("challenge-objective").ToList().Select(label => label.PlainText()).ToList();
        }

        public static TextShadow ResolvedTextShadow(VisualElement element)
        {
            return element.resolvedStyle.textShadow;
        }
    }
}
