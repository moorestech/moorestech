using Mooresmaster.Localization.Generated;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge
{
    /// <summary>
    /// 進行中チャレンジ HUD の面・位置・寸法・目標の並びと折り返しが Web の hudLayout.spec と同じことを確かめる（L1。1280×720）
    /// Verifies the current challenge HUD face, position, size, objective order and wrapping match the Web hudLayout.spec (L1; 1280x720)
    /// </summary>
    public class CurrentChallengeHudLayoutTest : ScreenTestHost
    {
        // 目標の間隔のトークンを局所で 7px に上書きするテスト用の USS（前例 PaperSurfaceTest の KnobSheetPath）
        // Test USS that locally overrides the objective-gap token to 7px (precedent: PaperSurfaceTest KnobSheetPath)
        private const string GapOverrideSheetPath = "Assets/Scripts/Client.Tests/Ui/Screens/Challenge/ChallengeHudGapOverride.uss";

        private static readonly string[] MultipleObjectives = { "石を採掘する", "石器をクラフトする", "木を伐採して拠点へ運ぶ" };
        private static readonly string[] MultipleLongObjectives =
        {
            "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
            "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
            "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
        };

        private CurrentChallengeHudView _view;

        [SetUp]
        public void MountView()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            _view = new CurrentChallengeHudView();
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/challenge/hudLayout.spec.ts">進行中チャレンジを内部キーを出さずインベントリ同族の面付きで表示する</web-test>
        [Test]
        public void 進行中チャレンジを内部キーを出さずhudの面付きで表示する()
        {
            Render(MultipleObjectives.Take(1).ToArray());
            var label = Find<Label>(_view, "challenge-hud-label");
            var surface = _view.Q<GamePanelSurface>("challenge-hud-surface");

            // 題は辞書の文で内部キーを出さない。目標は渡した文
            // The title is dictionary text without internal keys; the objective is the given text
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.Challenge.CurrentTitle), label.text);
            StringAssert.DoesNotContain("[!", label.text);
            CollectionAssert.AreEqual(new[] { "石を採掘する" }, ChallengeHudTestText.Objectives(_view));

            // 左上 24px・幅 278px・面の padding 14px・罫線 88px・文字の影・アンカー
            // Top-left 24px, width 278px, face padding 14px, rule 88px, text shadow and anchor
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-left"), _view.worldBound.x, 0.05f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-top"), _view.worldBound.y, 0.05f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-width"), _view.worldBound.width, 0.05f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--hud-panel-padding"), surface.resolvedStyle.paddingLeft, 0.05f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-rule-width"), _view.Q(className: "hud-section-heading__rule").worldBound.width, 0.05f);
            Assert.AreEqual(0.35f, ChallengeHudTestText.ResolvedTextShadow(_view).offset.x, 0.01f);
            Assert.AreEqual(0.8f, ChallengeHudTestText.ResolvedTextShadow(_view).color.a, 0.01f);
            Assert.IsTrue(_view.ClassListContains(AnchorIds.ChallengeCurrentHud));
            foreach (var element in _view.Query<VisualElement>().ToList()) Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name);
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="e2e/tests/challenge/hudLayout.spec.ts">面付きHUDは目標3件でもメニュー上端の安全帯に収まる</web-test>
        [Test]
        public void 面付きHUDは目標3件でもメニュー上端の安全帯に収まる()
        {
            Render(MultipleObjectives);
            Assert.AreEqual(3, ChallengeHudTestText.Objectives(_view).Count);
            Assert.LessOrEqual(_view.worldBound.yMax, ThemeTokenFile.ResolvePixels("--menu-upper-safe-area") + 0.05f);
        }

        /// <web-test file="e2e/tests/challenge/hudLayout.spec.ts">複数目標を受信順で表示し長文をHUD幅内へ折り返す</web-test>
        [Test]
        public void 複数目標を受信順で表示し長文をHUD幅内へ折り返す()
        {
            Render(MultipleObjectives);
            CollectionAssert.AreEqual(MultipleObjectives, ChallengeHudTestText.Objectives(_view));

            // 区切れない長文 1 件は HUD の本文幅の中で複数行に折り返す
            // A single unbroken long objective wraps into several lines inside the HUD content width
            Render(new[] { "VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHud" });
            var objective = _view.Q<Label>("challenge-objective");
            var lineHeight = ThemeTokenFile.ResolvePixels("--challenge-hud-objective-line-height");
            Assert.LessOrEqual(objective.worldBound.xMax, ContentRight() + 0.05f);
            Assert.GreaterOrEqual(objective.worldBound.height, lineHeight * 2f - 1f);
        }

        /// <web-test file="e2e/tests/challenge/hudLayout.spec.ts">複数の長文目標を受信順かつHUD幅内で表示する</web-test>
        [Test]
        public void 複数の長文目標を受信順かつHUD幅内で行送り20pxで表示する()
        {
            Render(MultipleLongObjectives);
            CollectionAssert.AreEqual(MultipleLongObjectives, ChallengeHudTestText.Objectives(_view));
            var objectives = _view.Query<Label>("challenge-objective").ToList();
            var lineHeight = ThemeTokenFile.ResolvePixels("--challenge-hud-objective-line-height");
            var gap = ThemeTokenFile.ResolvePixels("--challenge-hud-objective-gap");

            // どの目標も本文幅に収まり、高さは行高の整数倍（行送り 20px・1px 以内。設計書 A-48）
            // Every objective fits the content width and its height is a whole multiple of the line height (20px pitch within 1px; design A-48)
            foreach (var objective in objectives)
            {
                Assert.LessOrEqual(objective.worldBound.xMax, ContentRight() + 0.05f);
                var lines = Mathf.Round(objective.worldBound.height / lineHeight);
                Assert.GreaterOrEqual(lines, 1f);
                Assert.AreEqual(lines * lineHeight, objective.worldBound.height, 1f);
            }

            // 目標の間は --challenge-hud-objective-gap。画面 USS が --ui-gap へ渡すので、局所で上書きしても追従する
            // Objectives are --challenge-hud-objective-gap apart; the screen USS hands it to --ui-gap, so a local override follows
            Assert.AreEqual(gap, objectives[1].worldBound.yMin - objectives[0].worldBound.yMax, 0.05f);
            _view.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(GapOverrideSheetPath));
            _view.AddToClassList("challenge-hud-gap-override");
            simulate.FrameUpdate();
            Assert.AreEqual(7f, objectives[1].worldBound.yMin - objectives[0].worldBound.yMax, 0.05f);
        }

        private void Render(string[] objectives)
        {
            _view.Render(CurrentChallengeHudViewModel.Create(objectives.Select(UiText.FromVerbatim).ToArray(), false));
            simulate.FrameUpdate();
        }

        private float ContentRight()
        {
            var surface = _view.Q<GamePanelSurface>("challenge-hud-surface");
            return surface.worldBound.xMax - surface.resolvedStyle.paddingRight;
        }
    }
}
