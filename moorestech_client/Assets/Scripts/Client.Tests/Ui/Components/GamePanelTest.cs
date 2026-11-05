using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components
{
    /// <summary>
    /// GamePanel（default）の構造・トークン由来の寸法・レイアウトを本番テーマのパネルで確かめる（L1）
    /// Verifies GamePanel (default) structure, token-driven sizes and layout on a panel with the production theme (L1)
    /// </summary>
    public class GamePanelTest : UiTestHost
    {
        private const string PanelStyleSheetPath = "Assets/Scripts/Client.Ui/Components/GamePanel/GamePanel.uss";
        private const string TokensPath = "Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss";

        // Web の .panel の padding: 8px 10px 10px 28px の左右
        // Left and right of the Web .panel padding: 8px 10px 10px 28px
        private const float WebHorizontalPadding = 28f + 10f;

        private GamePanel _panel;

        [SetUp]
        public void AddPanel()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelStyleSheetPath);
            Assert.IsNotNull(sheet, PanelStyleSheetPath);
            _panel = new GamePanel(sheet);
            _panel.style.width = 400;
            _panel.style.height = 300;
            rootVisualElement.Add(_panel);
        }

        [Test]
        public void AddedChildGoesIntoBody()
        {
            var child = new Label("body");
            _panel.Add(child);
            // parent は contentContainer の持ち主（GamePanel）を返すので、物理の親を見る
            // parent returns the contentContainer owner (GamePanel), so check the physical parent
            Assert.IsTrue(child.hierarchy.parent.ClassListContains("game-panel__body"));
        }

        [Test]
        public void TitleRowAndRulesAppearOnlyAfterSetTitle()
        {
            simulate.FrameUpdate();
            var hidden = TitleRowElements();
            Assert.IsTrue(hidden.All(element => element.resolvedStyle.display == DisplayStyle.None));

            _panel.SetTitle(new Label("ポーズ"));
            simulate.FrameUpdate();
            Assert.IsTrue(TitleRowElements().All(element => element.resolvedStyle.display == DisplayStyle.Flex));
            Assert.AreEqual("ポーズ", _panel.Q<Label>(className: "game-panel__title").text);
        }

        [Test]
        public void FaceReadsEdgeFadeFromTokens()
        {
            simulate.FrameUpdate();
            var expected = float.Parse(ReadTokenBody("--panel-edge-fade").Replace("px", ""), CultureInfo.InvariantCulture);
            var face = _panel.Q(className: "game-panel__face");
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face.customStyle, "--game-panel-edge-fade", out var pixels));
            Assert.AreEqual(expected, pixels, 0.0001f);
        }

        [Test]
        public void DecorationsIgnorePicking()
        {
            Assert.AreEqual(PickingMode.Ignore, _panel.Q(className: "game-panel__face").pickingMode);
            Assert.AreEqual(PickingMode.Ignore, _panel.Q(className: "game-panel__bottom-deco").pickingMode);
        }

        [Test]
        public void BodyTakesPanelWidthMinusWebPadding()
        {
            _panel.SetTitle(new Label("ポーズ"));
            _panel.Add(new Label("line 1"));
            simulate.FrameUpdate();
            var body = _panel.Q(className: "game-panel__body");
            Assert.Greater(body.layout.width, 0f);
            Assert.AreEqual(_panel.layout.width - WebHorizontalPadding, body.layout.width, 1f);
            Assert.Greater(body.layout.height, 0f);
        }

        [Test]
        public void DrawingRaisesNoErrors()
        {
            _panel.SetTitle(new Label("ポーズ"));
            _panel.Add(new Label("line 1"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private VisualElement[] TitleRowElements()
        {
            return new[]
            {
                _panel.Q(className: "game-panel__deco-line--top"),
                _panel.Q(className: "game-panel__header"),
                _panel.Q(className: "game-panel__deco-line--bottom"),
            };
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
