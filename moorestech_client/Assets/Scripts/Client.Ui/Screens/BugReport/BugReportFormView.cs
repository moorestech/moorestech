using System.Collections.Generic;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BugReport
{
    /// <summary>
    /// バグ報告の種別・本文・状態行・配送行・救出操作・送信（Web の BugReportForm）。書きかけは BugReportDraft が持つ
    /// Bug-report kind, description, status line, delivery line, rescue controls and send (Web BugReportForm); the draft lives in BugReportDraft
    /// </summary>
    public sealed class BugReportFormView : VisualElement
    {
        private const string SubmitCommandName = "bug_report.submit";

        private readonly IPauseMenuCommands _commands;
        private readonly GameUiToastFeed _toasts;
        private readonly UiCommandResultToaster _toaster;
        private readonly BugReportDraft _draft;
        private readonly ModeSwitch<PlaytestReportKind> _kindSwitch;
        private readonly GameTextField _description;
        private readonly LocalizedLabel _status;
        private readonly LocalizedLabel _delivery;
        private readonly BugReportRecoveryControlsView _recovery;
        private readonly FormButton _sendButton;
        private BugReportCaptureStatus _capture;
        private bool _sending;

        public BugReportFormView(PauseMenuViewModel initial, IPauseMenuCommands commands, GameUiToastFeed toasts, UiCommandResultToaster toaster, BugReportDraft draft, IUiOsCursor osCursor)
        {
            _commands = commands;
            _toasts = toasts;
            _toaster = toaster;
            _draft = draft;
            AddToClassList("bug-report");

            // 種別の見出しと択一。押下は書きかけへ入れる
            // Kind caption and selector; a press goes into the draft
            var kindCaption = new LocalizedLabel();
            kindCaption.AddToClassList("bug-report__field-label");
            kindCaption.SetKey(LocalizationKeys.Ui.Playtest.ReportKind.Label);
            Add(kindCaption);
            _kindSwitch = new ModeSwitch<PlaytestReportKind>("bug-report-kind", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, KindOptions(), osCursor);

            _kindSwitch.OnOptionSelected.Subscribe(OnKindSelected);
            Add(_kindSwitch);

            // 本文は書きかけから復元し、打鍵ごとに書きかけへ戻す
            // The description is restored from the draft and written back on every keystroke
            _description = new GameTextField("bug-report-description", true, GameTextFieldLook.GaugeInset);
            _description.AddToClassList("bug-report__description");
            _description.SetPlaceholder(new UiText(LocalizationKeys.Ui.BugReport.Placeholder));
            _description.SetValueWithoutNotify(draft.Description);
            _description.OnValueChanged.Subscribe(OnDescriptionChanged);
            Add(_description);

            // 状態行は文があるときだけ木に載せる（キー無しで載せない）
            // The status line joins the tree only when it has a sentence, never without a key
            _status = new LocalizedLabel { name = "bug-report-status" };
            _status.AddToClassList("bug-report__status");
            _delivery = new LocalizedLabel { name = "bug-report-delivery" };
            _delivery.AddToClassList("bug-report__status");
            Add(_delivery);
            _recovery = new BugReportRecoveryControlsView(commands);
            Add(_recovery);

            // 送信は filled ボタン。文言は Refresh が入れる
            // Send is a filled button; Refresh sets the label
            _sendButton = new FormButton("bug-report-send", new UiText(BugReportFormText.SendLabel(draft.Kind)), FormButtonVariant.Filled);
            _sendButton.AddToClassList("bug-report__button");
            _sendButton.OnClicked.Subscribe(_ => SendAsync().Forget());
            Add(_sendButton);

            Apply(initial);
        }

        public void Apply(PauseMenuViewModel model)
        {
            _capture = model.Capture;
            _delivery.SetText(BugReportFormText.DescribeDelivery(model.Delivery));
            _recovery.Apply(model.Delivery, model.Export);
            Refresh();
        }

        private void OnKindSelected(PlaytestReportKind kind)
        {
            _draft.SetKind(kind);
            Refresh();
        }

        private void OnDescriptionChanged(string description)
        {
            _draft.SetDescription(description);
            Refresh();
        }

        private async UniTaskVoid SendAsync()
        {
            // 二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
            // A second click makes two boxes from one capture and raises two draft PRs for one report
            if (IsBlocked())
            {
                Debug.LogWarning($"[BugReportFormView] send ignored: sending={_sending} capture={_capture.Kind}");
                return;
            }

            // 押した時点の確保状態と種別で結果を伝える。書き出し中の欠損は manifest にだけ残す
            // Report with the capture state and kind at click time; gaps during writing remain only in the manifest
            var captureAtClick = _capture;
            var kind = _draft.Kind;
            var draftGeneration = _draft.Generation;
            _sending = true;
            Refresh();
            var outcome = await _commands.SubmitBugReport(_draft.Description.Trim(), kind);
            _sending = false;

            // トーストはToasterが決める
            // The toaster decides the toast
            _toaster.Report(SubmitCommandName, outcome);
            if (outcome is UiCommandRejected)
            {
                Refresh();
                return;
            }

            var missing = BugReportFormText.DescribeMissing(captureAtClick);
            if (missing == null) _toasts.Show(Localize.Get(BugReportFormText.SentToast(kind)), GameUiToastVariant.Info);
            else _toasts.Show(missing.Resolve(), GameUiToastVariant.Error);

            // 送信中にポーズを開き直して別の書きかけが始まっていたら、それを消さない
            // If the pause was reopened during the send and a different draft began, do not wipe it
            if (draftGeneration != _draft.Generation)
            {
                Debug.LogWarning("[BugReportFormView] draft was reset during the send; keeping the newer draft");
                Refresh();
                return;
            }

            _draft.Reset();
            _description.SetValueWithoutNotify(_draft.Description);
            Refresh();
        }

        private void Refresh()
        {
            // 応答待ちの間とホスト送信中は種別と本文を保護する
            // Protect the kind and description while awaiting the reply and while the host is writing
            var inputLocked = _sending || _capture.Kind == BugReportCaptureStatus.Submitting;
            _kindSwitch.SetSelected(_draft.Kind);
            _kindSwitch.SetEnabledAll(!inputLocked);
            _description.Field.SetEnabled(!inputLocked);

            // 状態行は1本だけ。送れない理由が無いときだけ欠損の一覧を出す
            // Only one status line: the missing list shows only when there is no blocking reason
            var statusLine = BugReportFormText.DescribeStatus(_capture, _sending, _draft.Kind) ?? BugReportFormText.DescribeMissing(_capture);
            if (statusLine == null) _status.RemoveFromHierarchy();
            else ShowStatus(statusLine);

            _sendButton.SetLabel(new UiText(BugReportFormText.SendLabel(_draft.Kind)));
            _sendButton.SetEnabled(!IsBlocked());
        }

        private void ShowStatus(UiText statusLine)
        {
            _status.SetText(statusLine);
            if (_status.parent == this) return;
            Insert(IndexOf(_description) + 1, _status);
        }

        private bool IsBlocked()
        {
            return _draft.Description.Trim().Length == 0 || _capture.Kind != BugReportCaptureStatus.Ready || _sending;
        }

        private static IReadOnlyList<ModeSwitchOption<PlaytestReportKind>> KindOptions()
        {
            return new List<ModeSwitchOption<PlaytestReportKind>>
            {
                new("bug-report-kind-bug", PlaytestReportKind.Bug, new UiText(LocalizationKeys.Ui.Playtest.ReportKind.Bug)),
                new("bug-report-kind-feedback", PlaytestReportKind.Feedback, new UiText(LocalizationKeys.Ui.Playtest.ReportKind.Feedback)),
            };
        }
    }
}
