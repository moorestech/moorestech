using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Notification;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.Common;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.DragDelete.Service
{
    // 確定とモード離脱で行が畳まれ処理中だけ残ることを確認
    // Verify lines fold on commit and mode exit, keeping pending
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

            // 離脱後は処理中の1行だけ
            // After re-entering only the pending line remains
            Service.ClearAll();
            Service.Update(0, false);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.DeleteResultPending.Key }, TooltipKeys());
        }

        [Test]
        public void 処理中は対象数によらず件数つきの1行にまとまる()
        {
            var targets = Enumerable.Range(0, 100).Select(_ => CreateDeferredTarget()).ToArray();
            CommitByHold(targets);
            Service.Update(0, false);

            // 100件でも行は1つで、件数を持つ
            // A hundred targets still make one line carrying the count
            var pendingLines = Tooltip.GetPresentation().Lines.Where(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.DeleteResultPending.Key).ToArray();
            Assert.AreEqual(1, pendingLines.Length);
            Assert.AreEqual("100", pendingLines[0].TextParams.Single(param => param.Name == "count").Value);

            // 応答が返った分だけ件数が減る
            // The count drops as responses arrive
            targets[0].Target.Complete(DeleteCompletionKind.Succeeded, null);
            Service.Update(0, false);
            Assert.AreEqual("99", Tooltip.GetPresentation().Lines.Single(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.DeleteResultPending.Key).TextParams.Single(param => param.Name == "count").Value);
        }

        [Test]
        public void 拒否は理由ごとに件数つきの行と通知1通にまとまる()
        {
            var notifications = new System.Collections.Generic.List<string>();
            using var subscription = ClientDIContext.ClientLocalNotificationSource.OnNotification.Subscribe(message => notifications.Add(message.MessageId));
            var targets = Enumerable.Range(0, 5).Select(_ => CreateDeferredTarget()).ToArray();
            CommitByHold(targets);
            for (var i = 0; i < 3; i++) targets[i].Target.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            for (var i = 3; i < 5; i++) targets[i].Target.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.RailHasVehicle);
            Service.Update(0, false);

            // 理由ごとに「件数の行→理由の行」の順で並ぶ
            // Each reason yields a count line followed by its reason line
            var lines = Tooltip.GetPresentation().Lines;
            CollectionAssert.AreEqual(new[]
            {
                LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key, LocalizationKeys.Ui.Delete.InventoryFull.Key,
                LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key, LocalizationKeys.Ui.Delete.RailHasVehicle.Key,
            }, lines.Select(line => line.Key.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "3", "2" }, lines.Where(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.DeleteResultRejected.Key).Select(line => line.TextParams.Single(param => param.Name == "count").Value).ToArray());
            CollectionAssert.AreEqual(new[] { "denied.deleteRejected", "denied.deleteRejected" }, notifications);
        }

        private HoverDenyReasonTarget CreateDeferredTarget()
        {
            var target = CreateTarget();
            target.Target.CompleteImmediately = false;
            return target;
        }

        // F長押しで確定し、離して次の押下に備える
        // Confirm by holding F, then release to accept the next press
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
