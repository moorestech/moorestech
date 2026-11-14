using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Skit.Parts.Choices;
using Client.Ui.Screens.Skit.Parts.Toolbar;
using Client.Ui.Screens.Skit.Parts.Window;
using Client.Ui.State.Skit;
using Client.Ui.State.Skit.Logic;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit
{
    /// <summary>
    /// スキットの画面。実画面いっぱいの透明な置き場に、会話窓・選択肢・背景の 1 行・ツールバーを載せる（Web SkitPresentation の .root）
    /// The skit screen; a transparent full-screen host carrying the window, choices, background line and toolbar (Web SkitPresentation .root)
    /// </summary>
    public sealed class SkitScreenView : VisualElement
    {
        private const string NotInteractiveWarning = "[skit.text] input rejected: presentation is not interactive";
        private const string WholeUiHiddenLog = "[skit.text] Enter ignored: the whole UI is hidden";

        private readonly ISkitCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly SkitDialogueWindow _window;
        private readonly SkitChoiceList _choices;
        private readonly SkitBackgroundLine _backgroundLine;
        private readonly SkitToolbar _toolbar;
        private readonly SkitTypewriter _typewriter;
        private SkitViewModel _model = SkitViewModel.Empty;
        private bool _interactive = true;
        private bool _wholeUiShown = true;
        private bool _hasRendered;

        public SkitScreenView(ISkitCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _commands = commands;
            _toaster = toaster;
            AddToClassList("skit-screen");
            pickingMode = PickingMode.Ignore;

            // Web と同順に重ねる
            // Stack in Web order
            _window = new SkitDialogueWindow(osCursor);
            _choices = new SkitChoiceList(commands, toaster, osCursor);
            _backgroundLine = new SkitBackgroundLine();
            _toolbar = new SkitToolbar(commands, toaster, osCursor);

            Add(_window);
            Add(_choices);
            Add(_backgroundLine);
            Add(_toolbar);

            // 自身を時計に字数を数え、変化で再描画
            // Count characters with self as clock; redraw on change
            _typewriter = new SkitTypewriter(this);
            _typewriter.OnVisibleCountChanged.Subscribe(_ => ShowRevealedText());
            _window.OnTextClicked.Subscribe(_ => HandleTextIntent());
            Render(SkitViewModel.Empty);
        }

        public void Render(SkitViewModel model)
        {
            // 載って最初の描画は復元として全文を出す（Web の初回接続・再接続の snapshot）
            // The first draw after mounting is a restore and shows everything (Web initial and reconnect snapshots)
            var restoring = !_hasRendered && panel != null;
            if (panel != null) _hasRendered = true;
            _model = model;
            _typewriter.Follow(model, restoring);

            // 出し分けは Surface だけを見る
            // Follow only the Surface
            _window.SetShown(model.Surface == SkitSurface.Window);
            _window.SetSpeaker(model.SpeakerName);
            _backgroundLine.SetShown(model.Surface == SkitSurface.BackgroundLine);
            _choices.Render(model);
            _toolbar.Render(model);
            ShowRevealedText();
        }

        // ポーズ中は全入力口を拾わなくし、表示と打ち出しは続ける（Web の interactive=false）
        // While paused every input path stops picking while drawing and typing continue (Web interactive=false)
        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            _window.SetInteractive(interactive);
            _choices.SetInteractive(interactive);
            _toolbar.SetInteractive(interactive);
        }

        // 全体非表示（Ctrl+U・カットシーン）の間は Enter を受けない。どの入力源から呼んでもここで止める（Web は会話 UI ごと外れている）
        // Refuse Enter while the whole UI is hidden (Ctrl+U, cutscene); stopped here whichever input calls it (on the Web the skit UI is unmounted)
        public void SetWholeUiShown(bool shown)
        {
            _wholeUiShown = shown;
        }

        // Enter は会話窓が出ている提示でだけクリックと同じ評価器を通す（Web は窓にフォーカスがあるときだけ）
        // Enter goes through the click evaluator only while the window shows (the Web only while the window has focus)
        public void HandleEnterKey()
        {
            if (!_wholeUiShown)
            {
                Debug.Log(WholeUiHiddenLog);
                return;
            }

            // silent-ok: 窓が無い提示では Web も Enter を受ける窓が無い / 窓の出し分けは SkitScreenViewTest が見る
            if (_model.Surface != SkitSurface.Window) return;
            HandleTextIntent();
        }

        private void HandleTextIntent()
        {
            if (!_interactive)
            {
                Debug.LogWarning(NotInteractiveWarning);
                return;
            }

            // 打ち出し中は全文、全文なら送り
            // Reveal all while typing; advance when complete
            var outcome = CurrentClickOutcome();
            if (outcome == SkitClickOutcome.Reveal) _typewriter.RevealAll();
            if (outcome == SkitClickOutcome.Advance) _toaster.Report(SkitCommands.AdvanceCommandName, _commands.Advance(_model.SessionId, _model.SceneRevision));
        }

        private void ShowRevealedText()
        {
            _window.SetBody(_model.Body, _typewriter.VisibleCount);
            _window.SetAdvanceMarkerShown(CurrentClickOutcome() == SkitClickOutcome.Advance);
            _backgroundLine.SetLine(_model.SpeakerName, _model.Body, _typewriter.VisibleCount);
        }

        // 現在のクリックの帰結（D-36）
        // Current click outcome (D-36)
        private SkitClickOutcome CurrentClickOutcome()
        {
            return SkitTypewriterRules.ClickOutcome(_typewriter.VisibleCount, _typewriter.TotalCount, _model.CanAdvance);
        }
    }
}
