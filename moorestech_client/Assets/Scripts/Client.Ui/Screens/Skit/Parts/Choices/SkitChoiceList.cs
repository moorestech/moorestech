using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Skit;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Choices
{
    /// <summary>
    /// 会話窓の上・右端に下から積む選択肢の列（Web SkitChoiceList）。同じ選択肢の並びが続く間は板を作り直さない
    /// Choices stacked upward just above the window at its right edge (Web SkitChoiceList); plates are not rebuilt while the same choices continue
    /// </summary>
    public sealed class SkitChoiceList : VisualElement
    {
        private readonly ISkitCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly IUiOsCursor _osCursor;
        private readonly List<SkitChoiceButton> _buttons = new();
        private IReadOnlyList<SkitChoiceItem> _shownChoices = new List<SkitChoiceItem>();
        private SkitViewModel _model = SkitViewModel.Empty;
        private bool _interactive = true;

        public SkitChoiceList(ISkitCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _commands = commands;
            _toaster = toaster;
            _osCursor = osCursor;
            AddToClassList("skit-choices");
            pickingMode = PickingMode.Ignore;
            UiDisplay.SetShown(this, false);
        }

        public void Render(SkitViewModel model)
        {
            _model = model;
            if (!SameChoices(model.Choices)) Rebuild(model.Choices);
            UiDisplay.SetShown(this, 0 < model.Choices.Count);

            #region Internal

            void Rebuild(IReadOnlyList<SkitChoiceItem> choices)
            {
                // 押下時の session・版で選ぶ
                // Select with the session and revision at press
                Clear();
                _buttons.Clear();
                for (var i = 0; i < choices.Count; i++)
                {
                    var choiceId = choices[i].ChoiceId;
                    var button = new SkitChoiceButton(choices[i], i == 0, _osCursor);
                    button.SetInteractive(_interactive);
                    button.OnClicked.Subscribe(_ => _toaster.Report(SkitCommands.SelectCommandName, _commands.Select(_model.SessionId, _model.SceneRevision, choiceId)));
                    _buttons.Add(button);
                    Add(button);
                }
                _shownChoices = choices;
            }

            bool SameChoices(IReadOnlyList<SkitChoiceItem> choices)
            {
                if (choices.Count != _shownChoices.Count) return false;
                for (var i = 0; i < choices.Count; i++)
                {
                    if (choices[i].ChoiceId != _shownChoices[i].ChoiceId || choices[i].Label != _shownChoices[i].Label) return false;
                }
                return true;
            }

            #endregion
        }

        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            foreach (var button in _buttons) button.SetInteractive(interactive);
        }
    }
}
