using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     理由行がプッシュ順で出ることを検証
    ///     Verify reason lines appear in push order
    /// </summary>
    public class PlacementFeedbackTooltipPresenterTest
    {
        private GameObject _tooltipObject;
        private MouseCursorTooltip _tooltip;

        [SetUp]
        public void SetUp()
        {
            // uGUI描画経路の文言解決が実辞書を引くため初期化しておく
            // Initialize the real dictionary because the uGUI render path resolves text through it
            Localize.Initialize();
            _tooltipObject = new GameObject("MouseCursorTooltip");
            _tooltipObject.SetActive(false);
            _tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
            SetField(_tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
            SetField(_tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
            _tooltipObject.SetActive(true);
            InvokePrivate(_tooltip, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tooltipObject);
            SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type targetType, string propertyName, object value)
        {
            targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        // Showが呼ばれたかは所有者トークンの書き換わりで観測する（Showの唯一の無条件な副作用のため）
        // Whether Show ran is observed through the owner token being overwritten, its only unconditional side effect
        private object GetCurrentOwner()
        {
            return typeof(MouseCursorTooltip).GetField("_currentOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_tooltip);
        }

        private void SetCurrentOwner(object owner)
        {
            typeof(MouseCursorTooltip).GetField("_currentOwner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_tooltip, owner);
        }

        [Test]
        public void 行があればその順で表示し無ければ非表示にする()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            feedback.Add(ElectricWireFeedbackLines.WireShortage());

            presenter.Present(feedback);

            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(2, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, presentation.Lines[1].Key.Key);

            feedback.Clear();
            presenter.Present(feedback);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 自分が表示していないときの空Presentは他者のツールチップを消さない()
        {
            MouseCursorTooltip.Instance.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            var presenter = new PlacementFeedbackTooltipPresenter();

            presenter.Present(new PlacementFeedback());

            Assert.IsTrue(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 表示中に他者へ上書きされたあとの空Presentは他者のツールチップを消さない()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            // 設置理由を出したあと別の書き手が所有権を取る。以降のPresenterのHideは無効
            // Another writer takes ownership after the placement reason is shown, so the presenter's Hide is inert
            MouseCursorTooltip.Instance.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            presenter.Present(new PlacementFeedback());

            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 同じFeedbackを積み直した再Presentが購読側へ届く()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            // 表示中に理由行が入れ替わるケース。使い回しバッファを直接渡すと同値判定で通知が止まる
            // Reason lines swap while shown; passing the reused buffer directly would stall the change notification
            var notifiedCount = 0;
            using var subscription = MouseCursorTooltip.Instance.OnPresentationChanged.Skip(1).Subscribe(_ => notifiedCount++);

            feedback.Clear();
            feedback.AddTooFar();
            presenter.Present(feedback);

            Assert.AreEqual(1, notifiedCount);
            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.AreEqual(1, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 内容が変わらないフレームはShowを呼ばない()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
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
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            presenter.Present(feedback);

            MouseCursorTooltip.Instance.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            presenter.Present(feedback);

            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.AreEqual(1, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].Key.Key);
        }

        [Test]
        public void 行が空のShowは非表示として扱う()
        {
            var owner = new TooltipOwner();
            MouseCursorTooltip.Instance.Show(owner, LocalizationKeys.Ui.Tooltip.HoldToGet);

            MouseCursorTooltip.Instance.Show(owner, System.Array.Empty<TooltipLine>());

            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
            Assert.AreEqual(0, MouseCursorTooltip.Instance.GetPresentation().Lines.Count);
        }
    }
}
