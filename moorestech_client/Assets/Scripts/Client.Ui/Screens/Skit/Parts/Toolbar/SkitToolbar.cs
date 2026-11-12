using Client.Ui.Components.Controls;
using Client.Ui.State.Skit;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Toolbar
{
    /// <summary>
    /// 実画面の右上に並ぶ自動・スキップ・UI 非表示と、UI 非表示中だけの復帰ボタン（Web SkitToolbar.tsx の SkitToolbar・SkitRestoreButton）
    /// Auto, skip and hide-UI in the screen's top-right, plus the restore button shown only while the UI is hidden (Web SkitToolbar.tsx SkitToolbar and SkitRestoreButton)
    /// </summary>
    public sealed class SkitToolbar : VisualElement
    {
        public const string ActiveToolUssClassName = "skit-toolbar__tool--active";
        private const string ToolUssClassName = "skit-toolbar__tool";
        private const string LeadToolUssClassName = "skit-toolbar__tool--lead";

        private readonly ISkitCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly IconButton _auto;
        private readonly IconButton _skip;
        private readonly IconButton _hideUi;
        private readonly IconButton _showUi;
        private SkitViewModel _model = SkitViewModel.Empty;

        public SkitToolbar(ISkitCommands commands, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _toaster = toaster;
            AddToClassList("skit-toolbar");
            pickingMode = PickingMode.Ignore;

            // 自動送りの ON/OFF は色替えで示す
            // Auto-advance on/off is shown by color swap
            _auto = AddTool("skit-tool-auto", SkitToolGlyphShape.Auto, true);
            _skip = AddTool("skit-tool-skip", SkitToolGlyphShape.Skip, false);
            _hideUi = AddTool("skit-tool-hide-ui", SkitToolGlyphShape.HideUi, false);
            _showUi = AddTool("skit-show-ui", SkitToolGlyphShape.ShowUi, true);

            // 押下時の session・版を付けて送る
            // Send with the session and revision at press
            _auto.OnClicked.Subscribe(_ => _toaster.Report(SkitCommands.SetAutoCommandName, _commands.SetAuto(_model.SessionId, _model.SceneRevision, !_model.AutoEnabled)));
            _skip.OnClicked.Subscribe(_ => _toaster.Report(SkitCommands.SkipCommandName, _commands.Skip(_model.SessionId, _model.SceneRevision)));
            _hideUi.OnClicked.Subscribe(_ => _toaster.Report(SkitCommands.SetUiHiddenCommandName, _commands.SetUiHidden(_model.SessionId, _model.SceneRevision, true)));
            _showUi.OnClicked.Subscribe(_ => _toaster.Report(SkitCommands.SetUiHiddenCommandName, _commands.SetUiHidden(_model.SessionId, _model.SceneRevision, false)));
        }

        public void Render(SkitViewModel model)
        {
            // ツールは窓面で、復帰は UI 非表示面でだけ出す
            // Tools on window surfaces; restore only on the hidden-UI surface
            _model = model;
            var controlsShown = model.Surface == SkitSurface.Window || model.Surface == SkitSurface.ToolbarOnly;
            SetShown(_auto, controlsShown);
            SetShown(_skip, controlsShown);
            SetShown(_hideUi, controlsShown);
            SetShown(_showUi, model.Surface == SkitSurface.RestoreOnly);

            // 押せるかは許可で決め、自動 ON だけシアン
            // Pressability follows allowance; only auto ON is cyan
            _auto.SetEnabled(model.CanSetAuto);
            _skip.SetEnabled(model.CanSkip);
            _hideUi.SetEnabled(model.CanSetUiHidden);
            _auto.EnableInClassList(ActiveToolUssClassName, model.AutoEnabled);
        }

        // ポーズ中は 4 つとも拾わず、背後のポーズへも抜けない位置に表示だけ残す（Web の .inputBlocked）
        // While paused none of the four pick, leaving only the display (Web .inputBlocked)
        public void SetInteractive(bool interactive)
        {
            var mode = interactive ? PickingMode.Position : PickingMode.Ignore;
            _auto.pickingMode = mode;
            _skip.pickingMode = mode;
            _hideUi.pickingMode = mode;
            _showUi.pickingMode = mode;
        }

        private IconButton AddTool(string testid, SkitToolGlyphShape shape, bool lead)
        {
            var button = new IconButton(testid, new SkitToolGlyph(shape));
            button.AddToClassList(ToolUssClassName);
            button.EnableInClassList(LeadToolUssClassName, lead);
            Add(button);
            return button;
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
