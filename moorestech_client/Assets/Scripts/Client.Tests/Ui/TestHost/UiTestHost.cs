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
    /// トークン・テーマ・フォントは本番 PanelSettings の themeStyleSheet（GameUiTheme.tss）から当たる
    /// Tokens, theme and fonts come from the production PanelSettings themeStyleSheet (GameUiTheme.tss)
    /// </remarks>
    public abstract class UiTestHost : UITestFixture
    {
        private const string GameUiPanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
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
    }
}
