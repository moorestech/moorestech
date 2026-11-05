using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// 本番と同じ寸法・テーマ・トークン USS を当てたパネルで UITK の View を試す土台
    /// Test base that hosts UITK views on a panel with the production size, theme and token USS
    /// </summary>
    /// <remarks>
    /// 拡張点: 継承し、[SetUp] か各テストで rootVisualElement（または UiLayerStack.Build(rootVisualElement) の層）へ View を Add し、simulate.FrameUpdate() でレイアウトしてから検証する
    /// Extension: inherit, Add the view to rootVisualElement (or a layer from UiLayerStack.Build(rootVisualElement)) in [SetUp] or a test, then call simulate.FrameUpdate() to lay out before asserting
    /// </remarks>
    public abstract class UiTestHost : UITestFixture
    {
        private const string GameUiPanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private const string TokenUssDirectory = "Assets/Scripts/Client.Ui/Foundation/Theme";
        private static readonly string[] TokenUssFileNames = { "Tokens.uss", "SkitTokens.uss", "SlotTokens.uss" };
        private static readonly Vector2 GameUiPanelSize = new(1280, 720);

        [OneTimeSetUp]
        public void SetUpGameUiTheme()
        {
            // テーマは基底の SetUp が各テスト前にパネルへ当てるので、最初のテストより前に渡す
            // The base SetUp applies the theme to the panel before each test, so hand it over before the first test
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(GameUiPanelSettingsPath);
            if (panelSettings == null) Assert.Fail($"PanelSettings not found: {GameUiPanelSettingsPath}");
            if (panelSettings.themeStyleSheet == null) Assert.Fail($"PanelSettings has no theme: {GameUiPanelSettingsPath}");

            themeStyleSheet = panelSettings.themeStyleSheet;
            panelSize = GameUiPanelSize;
        }

        [SetUp]
        public void SetUpTokenStyleSheets()
        {
            // 基底の TearDown がルートのスタイルシートを消すので、トークン USS はテストごとに当て直す
            // The base TearDown clears the root style sheets, so reapply the token USS for every test
            foreach (var fileName in TokenUssFileNames)
            {
                var path = $"{TokenUssDirectory}/{fileName}";
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet == null) Assert.Fail($"Token USS not found: {path}");
                rootVisualElement.styleSheets.Add(styleSheet);
            }
        }
    }
}
