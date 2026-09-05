using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     理由行がプッシュ順で出ることを検証
    ///     Verify reason lines appear in push order
    /// </summary>
    public class PlacementFeedbackTooltipPresenterTest
    {
        private MouseCursorTooltipState _tooltip;

        [SetUp]
        public void SetUp()
        {
            // uGUI描画経路の文言解決が実辞書を引くため初期化しておく
            // Initialize the real dictionary because the uGUI render path resolves text through it
            Localize.Initialize();
            _tooltip = new MouseCursorTooltipState();
        }

        [Test]
        public void 行があればその順で表示し無ければ非表示にする()
        {
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            ElectricWireFeedbackLines.ReportWireShortages(System.Array.Empty<ConstructionMaterialShortage>(), feedback);

            presenter.Present(feedback);

            var presentation = _tooltip.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(2, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, presentation.Lines[1].Key.Key);

            feedback.Clear();
            presenter.Present(feedback);
            Assert.IsFalse(_tooltip.GetPresentation().Visible);
        }

        [Test]
        public void 自分が表示していないときの空Presentは他者のツールチップを消さない()
        {
            _tooltip.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);

            presenter.Present(new PlacementFeedback());

            Assert.IsTrue(_tooltip.GetPresentation().Visible);
        }

        [Test]
        public void 表示中に他者へ上書きされたあとの空Presentは他者のツールチップを消さない()
        {
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            // 設置理由を出したあと別の書き手が所有権を取る。以降のPresenterのHideは無効
            // Another writer takes ownership after the placement reason is shown, so the presenter's Hide is inert
            _tooltip.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            presenter.Present(new PlacementFeedback());

            var presentation = _tooltip.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 同じFeedbackを積み直した再Presentが購読側へ届く()
        {
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            // 表示中に理由行が入れ替わるケース。使い回しバッファを直接渡すと同値判定で通知が止まる
            // Reason lines swap while shown; passing the reused buffer directly would stall the change notification
            var notifiedCount = 0;
            using var subscription = _tooltip.OnPresentationChanged.Skip(1).Subscribe(_ => notifiedCount++);

            feedback.Clear();
            feedback.AddTooFar();
            presenter.Present(feedback);

            Assert.AreEqual(1, notifiedCount);
            var presentation = _tooltip.GetPresentation();
            Assert.AreEqual(1, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 内容が変わらないフレームはShowを呼ばない()
        {
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            // Showは毎回新しい行配列を渡すため、配列が同一なら再Showは起きていない
            // Show always hands over a fresh line array, so an identical array proves no re-show happened
            var shownLines = _tooltip.GetPresentation().Lines;
            presenter.Present(feedback);

            Assert.AreSame(shownLines, _tooltip.GetPresentation().Lines);

            feedback.Clear();
            feedback.AddTooFar();
            presenter.Present(feedback);

            Assert.AreNotSame(shownLines, _tooltip.GetPresentation().Lines);
        }

        [Test]
        public void 他者に上書きされたあとは同じ内容でも出し直す()
        {
            var presenter = new PlacementFeedbackTooltipPresenter(_tooltip);
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            _tooltip.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            presenter.Present(feedback);

            var presentation = _tooltip.GetPresentation();
            Assert.AreEqual(1, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 行が空のShowは非表示として扱う()
        {
            var owner = new TooltipOwner();
            _tooltip.Show(owner, LocalizationKeys.Ui.Tooltip.HoldToGet);

            _tooltip.Show(owner, System.Array.Empty<TooltipLine>());

            Assert.IsFalse(_tooltip.GetPresentation().Visible);
            Assert.AreEqual(0, _tooltip.GetPresentation().Lines.Count);
        }
    }
}
