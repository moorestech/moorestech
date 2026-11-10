using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.TrainHud;
using Client.Ui.State.Routing;
using Client.Ui.State.Train;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.TrainHud
{
    /// <summary>
    /// 列車乗車 HUD が舞台の下中央に Paper の面で題と分岐を出すことを確かめる（L1。Web の train.spec の HUD の半分）
    /// Verifies the train riding HUD shows its title and branch on a Paper face at the bottom center of the stage (L1; the HUD half of Web train.spec)
    /// </summary>
    public class TrainRidingHudViewTest : ScreenTestHost
    {
        private VisualElement _stageLayer;
        private TrainRidingHudView _view;

        [SetUp]
        public void MountView()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            _stageLayer = stack.GetLayer(UiLayer.Screen, UiFamily.Stage);
            _view = new TrainRidingHudView();
            _stageLayer.Add(_view);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/train.spec.ts">乗車HUDと分岐選択を表示し、入れ子Pauseへ遷移する</web-test>
        [Test]
        public void 乗車中は題を出し分岐が1つなら分岐を出さない()
        {
            _view.Render(Riding(1, 0));
            var paper = Find<VisualElement>(_view, "train-riding-hud");
            Assert.IsTrue(IsShown(paper));
            StringAssert.Contains(Localize.Get(LocalizationKeys.Ui.TrainHud.Title), paper.Q<Label>(className: "train-riding-hud__title").text);
            Assert.IsFalse(IsShown(Find<Label>(_view, "train-branch-selection")));

            // 降りたら消える
            // Gone once off the train
            _view.Render(TrainRidingHudViewModel.Create(UiScreen.TrainHud, false, 1, 0));
            Assert.IsFalse(IsShown(paper));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 分岐候補が2以上なら選択中の分岐を番号と数で出す()
        {
            _view.Render(Riding(3, 1));
            var branch = Find<Label>(_view, "train-branch-selection");
            Assert.IsTrue(IsShown(branch));
            var expected = Localize.GetFormatted(LocalizationKeys.Ui.TrainHud.BranchSelection, new[] { new LocalizationTextParam("current", "2"), new LocalizationTextParam("count", "3") });
            Assert.AreEqual(expected, branch.text);
            StringAssert.Contains("2/3", branch.text);
        }

        [Test]
        public void 舞台の下端から112pxの中央にPaperの面で出て入力を奪わない()
        {
            _view.Render(Riding(3, 1));
            simulate.FrameUpdate();
            var paper = Find<VisualElement>(_view, "train-riding-hud");

            // 下端から 7rem（112px）、左右中央。padding 12px 16px、面と文字の色はトークン
            // 7rem (112px) above the bottom, centered horizontally; padding 12px 16px; face and text colors are tokens
            Assert.AreEqual(_stageLayer.worldBound.yMax - 112f, paper.worldBound.yMax, 0.05f);
            Assert.AreEqual(_stageLayer.worldBound.center.x, paper.worldBound.center.x, 0.05f);
            Assert.AreEqual(12f, paper.resolvedStyle.paddingTop, 0.05f);
            Assert.AreEqual(16f, paper.resolvedStyle.paddingLeft, 0.05f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--train-riding-hud-face"), paper.resolvedStyle.backgroundColor, "face");
            Assert.AreEqual(0.82f, paper.resolvedStyle.backgroundColor.a, 1e-3f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-gray-1"), paper.resolvedStyle.color, "text");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-gray-1"), Find<Label>(_view, "train-branch-selection").resolvedStyle.color, "branch text");
            Assert.IsTrue(paper.ClassListContains(AnchorIds.TrainHudStatus));
            foreach (var element in _view.Query<VisualElement>().ToList()) Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name);
        }

        private static TrainRidingHudViewModel Riding(int branchCandidateCount, int selectedBranchIndex)
        {
            return TrainRidingHudViewModel.Create(UiScreen.TrainHud, true, branchCandidateCount, selectedBranchIndex);
        }
    }
}
