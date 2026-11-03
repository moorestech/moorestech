using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Notification;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.Common;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.DragDelete.Service
{
    // 撤去結果の行が次の確定とモード離脱で畳まれ、処理中だけ残ることを確かめる
    // Verify result lines fold on the next commit and on mode exit, keeping only pending ones
    public class DeleteResultFeedbackClearTest : DeleteInputFixture
    {
        private ClientLocalNotificationSource _previousNotificationSource;

        public override void Setup()
        {
            base.Setup();
            // 拒否・未確認の通知先を差し替える
            // Swap in a notification sink for rejected/unconfirmed outcomes
            _previousNotificationSource = ClientDIContext.ClientLocalNotificationSource;
            TestReflection.SetStaticProperty(typeof(ClientDIContext), nameof(ClientDIContext.ClientLocalNotificationSource), new ClientLocalNotificationSource());
        }

        public override void TearDown()
        {
            TestReflection.SetStaticProperty(typeof(ClientDIContext), nameof(ClientDIContext.ClientLocalNotificationSource), _previousNotificationSource);
            base.TearDown();
        }

        [Test]
        public void 拒否の行は次の撤去確定で消え新しい処理中の行は残る()
        {
            var rejected = CreateDeferredTarget();
            CommitByHold(rejected);
            rejected.Target.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            Service.Update(0, false);
            CollectionAssert.Contains(TooltipKeys(), LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key);

            // 別の対象を確定すると前回の拒否は消える
            // Committing another target drops the previous rejection
            var next = CreateDeferredTarget();
            CommitByHold(next);
            Service.Update(0, false);
            var keys = TooltipKeys();
            CollectionAssert.DoesNotContain(keys, LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key);
            CollectionAssert.Contains(keys, LocalizationKeys.Ui.Tooltip.DeleteResultPending.Key);
        }

        [Test]
        public void モード離脱で拒否と未確認の行が消え処理中の行は残る()
        {
            var rejected = CreateDeferredTarget();
            var unconfirmed = CreateDeferredTarget();
            var pending = CreateDeferredTarget();
            CommitByHold(rejected, unconfirmed, pending);
            rejected.Target.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            unconfirmed.Target.Complete(DeleteCompletionKind.Unconfirmed, null);
            Service.Update(0, false);
            var before = TooltipKeys();
            CollectionAssert.Contains(before, LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key);
            CollectionAssert.Contains(before, LocalizationKeys.Ui.Tooltip.DeleteResultUnconfirmed.Key);

            // 破壊モードを抜けて戻ると処理中の1行だけになる
            // After leaving and re-entering delete mode only the pending line remains
            Service.ClearAll();
            Service.Update(0, false);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.DeleteResultPending.Key }, TooltipKeys());
        }

        private HoverDenyReasonTarget CreateDeferredTarget()
        {
            var target = CreateTarget();
            target.Target.CompleteImmediately = false;
            return target;
        }

        // 選択してF長押しで確定し、Fを離して次の押下を受けられる状態へ戻す
        // Select, confirm by holding F, then release F so the next press is accepted
        private void CommitByHold(params HoverDenyReasonTarget[] targets)
        {
            Selection.ApplyBoxTargets(targets, DeleteSelectionIntent.Add);
            Frame(false, new[] { Key.F });
            Service.Update(10f, false);
            Assert.IsFalse(Selection.HasSelection());
            Frame(false, Array.Empty<Key>());
            Service.Update(0, false);
        }

        private string[] TooltipKeys()
        {
            var presentation = Tooltip.GetPresentation();
            return presentation.Visible ? presentation.Lines.Select(line => line.Key.Key).ToArray() : Array.Empty<string>();
        }
    }
}
