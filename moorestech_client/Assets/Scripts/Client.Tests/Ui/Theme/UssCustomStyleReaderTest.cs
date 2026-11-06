using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 部品クラスで受け直したトークンを customStyle から読めることを確かめる（L1）
    /// Verifies tokens re-declared in a component class can be read from customStyle (L1)
    /// </summary>
    public class UssCustomStyleReaderTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleProbe.uss";
        private const string TokensPath = "Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss";

        private VisualElement _probe;

        [SetUp]
        public void AddProbe()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(sheet, ProbePath);
            rootVisualElement.styleSheets.Add(sheet);
            _probe = new VisualElement();
            _probe.AddToClassList("uss-custom-style-probe");
            rootVisualElement.Add(_probe);
            simulate.FrameUpdate();
        }

        [Test]
        public void ReadsPixelsFromRedeclaredToken()
        {
            var expected = float.Parse(ReadTokenBody("--panel-edge-fade").Replace("px", ""), CultureInfo.InvariantCulture);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(_probe.customStyle, "--probe-edge-fade", out var pixels));
            Assert.AreEqual(expected, pixels, 0.0001f);
        }

        [Test]
        public void ReadsMillisecondsFromRedeclaredToken()
        {
            UssTokenValue.TryParseMilliseconds(ReadTokenBody("--notification-enter-duration"), out var expected);
            Assert.IsTrue(UssCustomStyleReader.TryReadMilliseconds(_probe.customStyle, "--probe-enter", out var milliseconds));
            Assert.AreEqual(expected, milliseconds, 0.0001f);
        }

        [Test]
        public void ReadsColorFromRedeclaredToken()
        {
            var match = Regex.Match(ReadTokenBody("--surface-navy"), @"rgba\(\s*(\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\s*\)");
            Assert.IsTrue(match.Success);
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(_probe.customStyle, "--probe-face", out var color));
            Assert.AreEqual(float.Parse(match.Groups[1].Value) / 255f, color.r, 0.002f);
            Assert.AreEqual(float.Parse(match.Groups[2].Value) / 255f, color.g, 0.002f);
            Assert.AreEqual(float.Parse(match.Groups[3].Value) / 255f, color.b, 0.002f);
            Assert.AreEqual(float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture), color.a, 0.002f);
        }

        [Test]
        public void 単位なしの数をfloatで読める()
        {
            // --title-scale-x をCustomStyleResolvedEventで読む
            // Read --title-scale-x in CustomStyleResolvedEvent
            var element = new VisualElement();
            element.AddToClassList("uss-custom-style-probe");
            var read = false;
            var scale = 0f;
            element.RegisterCallback<CustomStyleResolvedEvent>(evt => read = UssCustomStyleReader.TryReadNumber(evt.customStyle, "--probe-title-scale", out scale));
            rootVisualElement.Add(element);
            simulate.FrameUpdate();
            Assert.IsTrue(read);
            Assert.AreEqual(0.947f, scale, 0.0001f);
        }

        [Test]
        public void RootTokenIsNotInheritedIntoChildCustomStyle()
        {
            // :root のトークンは子の customStyle に出ない（受け直しが要る根拠）
            // :root tokens do not reach a child's customStyle (the reason re-declaring is required)
            var plain = new VisualElement();
            rootVisualElement.Add(plain);
            simulate.FrameUpdate();
            Assert.IsFalse(plain.customStyle.TryGetValue(new CustomStyleProperty<string>("--panel-edge-fade"), out _));
        }

        [Test]
        public void キーワードを文字列で読める()
        {
            // 変種 A の前提
            // The premise of variant A
            Assert.IsTrue(UssCustomStyleReader.TryReadKeyword(_probe.customStyle, "--probe-keyword", out var keyword));
            Assert.AreEqual("inset", keyword);
        }

        private static string ReadTokenBody(string tokenName)
        {
            var text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), TokensPath));
            var match = Regex.Match(text, Regex.Escape(tokenName) + @":\s*([^;]+);");
            Assert.IsTrue(match.Success, tokenName);
            return Regex.Replace(match.Groups[1].Value, @"/\*.*?\*/", "").Trim();
        }
    }
}
