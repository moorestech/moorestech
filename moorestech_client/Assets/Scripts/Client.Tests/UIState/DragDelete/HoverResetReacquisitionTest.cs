using System;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class HoverResetReacquisitionTest
    {
        private const string SuppressionWarning = "[DragDelete] delete preview suppressed because the target's previous reset did not complete";

        [Test]
        public void IdleHoverBeforeNextExitDoesNotReacquireFailedTarget()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { ThrowOnReset = true };
            preview.Hover(broken);
            Assert.Throws<InvalidOperationException>(() => preview.Release());

            // 次のG押下まで同じ照準の入力なしフレームを挟む
            // Keep aiming at the target during idle frames before pressing G again
            LogAssert.Expect(LogType.Warning, SuppressionWarning);
            preview.Hover(broken);
            preview.Hover(broken);
            Assert.IsNull(preview.Current);
            Assert.DoesNotThrow(() => preview.Release());
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(1, broken.SetPreviewCount);
        }

        [Test]
        public void MultipleFailedTargetsStaySuppressedAcrossOtherHovers()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var first = new FakeDeleteTarget { ThrowOnReset = true };
            var second = new FakeDeleteTarget { ThrowOnReset = true };
            preview.Hover(first);
            Assert.Throws<InvalidOperationException>(() => preview.Release());
            preview.Hover(second);
            Assert.Throws<InvalidOperationException>(() => preview.Release());

            // A失敗からB失敗を経ても両者を再取得しない
            // Neither failed target is reacquired after failures on both A and B
            LogAssert.Expect(LogType.Warning, SuppressionWarning);
            preview.Hover(first);
            LogAssert.Expect(LogType.Warning, SuppressionWarning);
            preview.Hover(second);
            preview.Hover(first);
            preview.Hover(second);
            Assert.IsNull(preview.Current);
            Assert.DoesNotThrow(() => preview.Release());
            Assert.AreEqual(1, first.ResetCount);
            Assert.AreEqual(1, second.ResetCount);
        }

        [Test]
        public void SuccessfulResetAllowsSameTargetToBeHoveredAgain()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget();
            preview.Hover(target);
            preview.Release();

            // 正常な対象は次のホバーで再表示できる
            // A healthy target can show its preview on a later hover
            preview.Hover(target);
            Assert.AreSame(target, preview.Current);
            preview.Release();
            Assert.AreEqual(2, target.SetPreviewCount);
            Assert.AreEqual(2, target.ResetCount);
        }
    }
}
