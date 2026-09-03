using System;
using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     取得物なしの対象は従来文言を選ぶ
    ///     Verify the focus state picks the right transition and the nameless sentence for yield-less targets
    /// </summary>
    public class MiningFocusStateTest : MiningFocusStateTestFixture
    {
        [Test]
        public void 採掘対象でなくなったらIdleへ戻る()
        {
            var next = RunFocusState(MiningStartOutcome.Unavailable, new MiningFocusState(), Array.Empty<Guid>());

            Assert.IsInstanceOf<MiningIdleState>(next);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 手掘り不可の対象には掘れない旨を提示してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.HandMiningNotAllowed, focusState);

            // 掘れないことを示す文言が本PRの目的なので、キーごと固定する
            // Declaring it unmineable is this PR's goal, so pin the very key
            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CannotHandMine.Key, ShownTooltipKey());
        }

        [Test]
        public void 装備が合わない対象には必要ツールを提示してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.ToolMismatch, focusState);

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.RequiredItems.Key, ShownTooltipKey());
        }

        [Test]
        public void 採掘可能でも未クリックなら押下を促してフォーカスを維持する()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.Ready, focusState);

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, ShownTooltipKey());
        }

        [Test]
        public void 取得物のないPickUp対象には従来の単クリック文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.InstantPickUp, focusState);

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PickUpInteract.Key, ShownTooltipKey());
        }

        [Test]
        public void PickUp対象はF押下で完了状態へ抜けツールチップを畳む()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusStateWithInteractPressed(MiningStartOutcome.InstantPickUp, focusState);

            Assert.IsInstanceOf<MiningCompleteState>(next);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }
    }
}
