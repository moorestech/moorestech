using System;
using Client.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// モーダル 1 件の中身（Web ModalBody）。要求ごとに作り直し、入力の持ち越しを構造で無くす（Web の key={id}）。確定・ESC・幕の押下をすべて自分で IModalCommands へ返す（設計書 A-8）
    /// One modal's contents (Web ModalBody); rebuilt per request so input never carries over (Web key={id}). It returns confirm, ESC and backdrop presses to IModalCommands itself (design A-8)
    /// </summary>
    public sealed class ModalView : VisualElement
    {
        private readonly ModalRequest _request;
        private readonly IModalCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly IObservable<Unit> _backdropClicked;
        private readonly FormButton _button;
        private bool _confirmable;
        private string _confirmedText;
        private IDisposable _backdropSubscription;

        public ModalView(ModalRequest request, IModalCommands commands, UiCommandResultToaster toaster, IObservable<Unit> backdropClicked)
        {
            _request = request;
            _commands = commands;
            _toaster = toaster;
            _backdropClicked = backdropClicked;
            AddToClassList("modal-view");

            // 題・本文（Web Modal.Title fz=h4・Text size=sm c=dimmed mb=lg）。文は要求側が解いた文なので FromVerbatim（A-40）
            // Title and message (Web Modal.Title fz=h4, Text size=sm c=dimmed mb=lg); requester-resolved text, so FromVerbatim (A-40)
            var frame = new DialogFrame("modal");
            frame.SetTitle(UiText.FromVerbatim(request.Title));
            Add(frame);
            var message = new LocalizedLabel { name = "modal-message" };
            message.AddToClassList(UiTypographyClass.TextSm);
            message.AddToClassList(UiTypographyClass.TextDimmed);
            message.AddToClassList("modal-view__message");
            message.SetText(UiText.FromVerbatim(request.Message));
            frame.Add(message);

            // 入力欄は入力付きのみ、ボタンは全幅
            // The field only for input requests; the button spans the width
            if (request.RequiresInput) frame.Add(CreateInput());
            _button = new FormButton("modal-button", UiText.FromVerbatim(request.ButtonText), ModalButtonVariants.From(ModalLogic.ConfirmButtonTone(request.Variant)));
            _button.OnClicked.Subscribe(_ => Confirm());
            frame.AddAction(_button);
            SetEnteredText("");

            // 幕の押下は載っている間だけ聞く。フォーカスの無い ESC は聞かない（既存のゲーム入力の経路が取り消す。計画 D-11・設計書 A-83）
            // Listen for backdrop presses only while attached; focus-less ESC is not heard here (the existing game input path cancels; plan D-11, design A-83)
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        private GameTextField CreateInput()
        {
            var input = new GameTextField("modal-input", false, GameTextFieldLook.MantineInput);
            input.AddToClassList("modal-view__input");
            input.OnValueChanged.Subscribe(SetEnteredText);

            // 入力欄の ESC は GameUiInputRules が消費してフォーカスを外す。その外れを見て 1 回で取り消す（Web の Mantine と同じ 1 回。計画 D-11）
            // GameUiInputRules consumes ESC in the field and drops focus; seeing that blur, cancel in one press (one press like Mantine on the Web; plan D-11)
            input.Field.RegisterCallback<FocusOutEvent>(_ => CancelIfEscapeBlurred());

            // 表示時に入力欄へフォーカス（Web autoFocus）。載った直後はスタイルが未解決なので次のフレームで当てる
            // Focus the field when shown (Web autoFocus); styles are unresolved right after attaching, so apply next frame
            input.RegisterCallback<AttachToPanelEvent>(_ => input.schedule.Execute(() => input.Field.Focus()));
            return input;
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            _backdropSubscription = _backdropClicked.Subscribe(_ => Respond(ModalResponse.Cancel(_request)));
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            _backdropSubscription?.Dispose();
            _backdropSubscription = null;
        }

        // 可否と確定の文は文の変化ごとに 1 回だけ導き、ボタンと確定の両方がそれを読む（ModalLogic.TryGetConfirmableText）
        // Derive eligibility and the confirmed text once per text change; both the button and Confirm read it (ModalLogic.TryGetConfirmableText)
        private void SetEnteredText(string text)
        {
            _confirmable = ModalLogic.TryGetConfirmableText(_request, text, out _confirmedText);
            _button.SetEnabled(_confirmable);
        }

        private void Confirm()
        {
            if (!_confirmable)
            {
                Debug.LogWarning($"[Modal] confirm ignored for request {_request.Id}: entered text is whitespace only");
                return;
            }
            Respond(ModalResponse.Confirm(_request, _confirmedText));
        }

        private void CancelIfEscapeBlurred()
        {
            // silent-ok: ESC 以外の外れ（クリック・閉じたとき）と変換中の ESC は取り消さない / ModalScreenPresenterTest「入力欄のESCは1回で取り消す」「欄の外を押しても閉じない」「変換中のESCでは取り消さない」が見る
            if (!UiInputExclusivity.IsEscapeBlurredTextFieldThisFrame) return;
            Respond(ModalResponse.Cancel(_request));
        }

        private void Respond(ModalResponse response)
        {
            _toaster.Report(ModalCommands.RespondCommandName, _commands.Respond(response));
        }
    }
}
