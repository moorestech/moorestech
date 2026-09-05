using System.Reflection;
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

        // Showが呼ばれたかは所有者トークンの書き換わりで観測する（Showの唯一の無条件な副作用のため）
        // Whether Show ran is observed through the owner token being overwritten, its only unconditional side effect
        private object GetCurrentOwner()
        {
            return typeof(MouseCursorTooltipState).GetField("_currentOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_tooltip);
        }

        private void SetCurrentOwner(object owner)
        {
            typeof(MouseCursorTooltipState).GetField("_currentOwner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_tooltip, owner);
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

            // 番兵トークンを置き、再PresentでShowが走れば上書きされる状態にする
            // Plant a sentinel token so that a Show during the re-present would overwrite it
            var sentinel = new TooltipOwner();
            SetCurrentOwner(sentinel);
            presenter.Present(feedback);

            Assert.AreSame(sentinel, GetCurrentOwner());

            feedback.Clear();
            feedback.AddTooFar();
            presenter.Present(feedback);

            Assert.AreNotSame(sentinel, GetCurrentOwner());
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
