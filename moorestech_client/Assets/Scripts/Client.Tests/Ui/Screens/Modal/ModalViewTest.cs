using System;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// モーダルの中身（題・本文・入力・ボタン）と確定の可否・空白の除去・色（L1。Web modal.spec.ts）
    /// Modal contents (title, message, input, button), confirm eligibility, trimming and colour (L1; Web modal.spec.ts)
    /// </summary>
    public class ModalViewTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/modal.spec.ts">接続後にモーダルが描画される</web-test>
        [Test]
        public void 確認の要求は題と本文とボタンを描き入力欄を持たない()
        {
            var view = Mount(new ModalView(ModalRequestSamples.Confirm(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "modal")));
            StringAssert.EndsWith("確認", Find<Label>(view, "modal-title").text);
            StringAssert.EndsWith("これは確認ダイアログです", Find<Label>(view, "modal-message").text);
            var button = Find<FormButton>(view, "modal-button");
            StringAssert.EndsWith("OK", button.Q<Label>(className: FormButton.LabelUssClassName).text);
            Assert.IsTrue(button.enabledSelf);
            Assert.IsNull(view.Q<TextField>("modal-input"));
        }

        /// <web-test file="e2e/tests/modal.spec.ts">BP名入力fixtureは入力後の確定値を送る</web-test>
        [Test]
        public void BP名の入力は空のうち押せず前後の空白を除いた名前で確定する()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), commands, Toaster, Observable.Never<Unit>()));
            var responses = commands.Responses;
            simulate.FrameUpdate();

            var input = Find<TextField>(view, "modal-input");
            var button = Find<FormButton>(view, "modal-button");
            Assert.IsTrue(IsShown(input));
            Assert.IsFalse(button.enabledSelf);

            input.value = "  参照拠点  ";
            simulate.FrameUpdate();
            Assert.IsTrue(button.enabledSelf);
            simulate.Click(button.worldBound.center);

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("blueprint-name", responses[0].RequestId);
            Assert.AreEqual(ModalResult.Confirm, responses[0].Result);
            Assert.AreEqual("参照拠点", responses[0].Text);
        }

        [Test]
        public void 入力なしの確定は文を運ばない()
        {
            // TryGetConfirmableText は入力なしで確定の文を null にし、View はその null をそのまま応答へ渡す
            // TryGetConfirmableText leaves the confirmed text null without input, and the view passes that null straight to the reply
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.Confirm(), commands, Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            simulate.Click(Find<FormButton>(view, "modal-button").worldBound.center);

            Assert.AreEqual(1, commands.Responses.Count);
            Assert.AreEqual(ModalResult.Confirm, commands.Responses[0].Result);
            Assert.IsNull(commands.Responses[0].Text);
            CollectionAssert.IsEmpty(ShownToasts);
        }

        [Test]
        public void 空白だけの入力では押せず押しても確定を流さない()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), commands, Toaster, Observable.Never<Unit>()));
            var responses = commands.Responses;
            simulate.FrameUpdate();

            Find<TextField>(view, "modal-input").value = " \t　";
            simulate.FrameUpdate();
            var button = Find<FormButton>(view, "modal-button");
            Assert.IsFalse(button.enabledSelf);
            simulate.Click(button.worldBound.center);
            CollectionAssert.IsEmpty(responses);
        }

        [Test]
        public void 入力付きの要求は表示の後に入力欄へフォーカスが入る()
        {
            // Web の TextInput autoFocus。パネルに載った次のフレームで当てる（計画 D-13）
            // Web TextInput autoFocus; applied on the frame after attaching to the panel (plan D-13)
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var input = Find<TextField>(view, "modal-input");
            var focused = view.focusController.focusedElement as VisualElement;
            Assert.IsTrue(focused == input || (focused != null && input.Contains(focused)), focused == null ? "null" : focused.name);
        }

        [Test]
        public void エラーは危険色で確認は塗りのボタンになる()
        {
            // 色の写しは ModalButtonVariants の 1 か所（設計書 A-28）
            // The colour mapping lives only in ModalButtonVariants (design A-28)
            Assert.AreEqual(FormButtonVariant.Filled, ModalButtonVariants.From(ModalButtonTone.Blue));
            Assert.AreEqual(FormButtonVariant.Danger, ModalButtonVariants.From(ModalButtonTone.Red));
            var error = Mount(new ModalView(ModalRequestSamples.Error(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            Assert.IsTrue(Find<FormButton>(error, "modal-button").ClassListContains(FormButton.DangerUssClassName));
        }
    }
}
