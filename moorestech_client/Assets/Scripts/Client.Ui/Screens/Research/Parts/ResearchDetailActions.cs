using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ボタンと不可の理由
    /// Research button and unavailable reason
    /// </summary>
    public sealed class ResearchDetailActions : VisualElement
    {
        private readonly IResearchTreeCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly RecipeActionButton _button;
        private readonly LocalizedLabel _reason;
        private Guid _researchGuid;

        public ResearchDetailActions(IResearchTreeCommands commands, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _toaster = toaster;
            AddToClassList("research-tree__actions");
            pickingMode = PickingMode.Ignore;
            _button = new RecipeActionButton("research-button", new UiText(LocalizationKeys.Ui.Research.Action), RecipeActionButtonVariant.Research);
            _button.OnClicked.Subscribe(_ => CompleteResearch());
            _reason = new LocalizedLabel { name = "research-detail-reason" };
            _reason.AddToClassList("research-tree__reason");
            _reason.SetText(new UiText(LocalizationKeys.Ui.Research.MissingItems));
            Add(_button);
            Add(_reason);
        }

        // 結果を待ってトースト係へ渡す（前例 SpecialViewKit.Run。A-262）
        // Await the result and hand it to the toaster (precedent SpecialViewKit.Run; A-262)
        private void CompleteResearch()
        {
            _commands.CompleteResearch(_researchGuid).ContinueWith(result => _toaster.Report(ResearchTreeCommands.CompleteResearchCommandName, result)).Forget();
        }

        public void Render(Guid researchGuid, ResearchButtonState button, bool nodeStatesReceived)
        {
            // testidはノードごと
            // The testid is per node
            _researchGuid = researchGuid;
            _button.name = $"research-button-{researchGuid}";
            _button.SetLabel(new UiText(button.Completed ? LocalizationKeys.Ui.Research.Completed : LocalizationKeys.Ui.Research.Action));
            _button.SetEnabled(button.Interactable);

            // 理由は状態受信後の未完了かつ不可のみ（受信前の既定は嘘の理由になる）
            // Reason only after states arrive, when not completed and unavailable (the pre-receipt default would lie)
            var showsReason = nodeStatesReceived && !button.Completed && !button.Interactable;
            _reason.style.display = showsReason ? DisplayStyle.Flex : DisplayStyle.None;
            _reason.SetText(new UiText(button.TooltipKey));
        }
    }
}
