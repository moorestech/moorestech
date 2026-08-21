using System.Reflection;
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
    ///     設置理由の行がカーソルツールチップへプッシュ順で出ることを検証
    ///     Verify placement reason lines reach the cursor tooltip in push order
    /// </summary>
    public class PlacementFeedbackTooltipPresenterTest
    {
        private GameObject _tooltipObject;

        [SetUp]
        public void SetUp()
        {
            // 文言解決は実辞書を通す（Show内でLocalize.GetLegacyを呼ぶため）
            // Resolve text through the real dictionary (Show calls Localize.GetLegacy)
            Localize.Initialize();
            _tooltipObject = new GameObject("MouseCursorTooltip");
            _tooltipObject.SetActive(false);
            var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
            SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
            SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
            _tooltipObject.SetActive(true);
            InvokePrivate(tooltip, "Awake");
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

        [Test]
        public void 行があればその順で表示し無ければ非表示にする()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            feedback.AddWireShortage();

            presenter.Present(feedback);

            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(2, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, presentation.Lines[1].TextKey);

            feedback.Clear();
            presenter.Present(feedback);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 自分が表示していないときの空Presentは他者のツールチップを消さない()
        {
            MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.HoldToGet);
            var presenter = new PlacementFeedbackTooltipPresenter();

            presenter.Present(new PlacementFeedback());

            Assert.IsTrue(MouseCursorTooltip.Instance.GetPresentation().Visible);
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
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, presentation.Lines[0].TextKey);
        }

        [Test]
        public void 電線コスト0は行を追加しない()
        {
            var feedback = new PlacementFeedback();
            feedback.AddWireCost(0);
            feedback.AddWireCost(3);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, feedback.Lines[0].TextKey);
            CollectionAssert.AreEqual(new[] { "3" }, feedback.Lines[0].TextParams);
        }
    }
}
