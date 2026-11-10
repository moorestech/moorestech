using Client.Localization;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 搬入停止欄
    /// Inflow-stall section: the stall reason in red naming the waiting item; a non-red fetch-state line while unavailable
    /// </summary>
    public sealed class MachineInputStallSectionView : VisualElement
    {
        private readonly GameContentPresentation _content;
        private readonly BlockInfoUnavailableLine _unavailable = new("machine-input-stall-fetch-state", LocalizationKeys.Ui.BlockInventory.MachineInputStallInfoLabel);
        private readonly LackHighlightText _stall = new("machine-input-stall", LackHighlightTextSize.Xs);

        public MachineInputStallSectionView(GameContentPresentation content)
        {
            _content = content;
            name = "machine-input-stall-section-view";
        }

        public void Render(ContextEntry<MachineInputStall> entry)
        {
            if (entry is UnavailableContextEntry<MachineInputStall> unavailable)
            {
                _unavailable.RenderInto(this, unavailable.FetchState);
                return;
            }
            var stall = ((FetchedContextEntry<MachineInputStall>)entry).Value;
            var itemName = _content.ItemName(stall.WaitingItemId).Resolve();
            _stall.Render(new UiText(MachineInputStallLogic.MessageKey(stall.Reason), new[] { new LocalizationTextParam("itemName", itemName) }), true);
            ContextRowList.MountOnly(this, _stall);
        }
    }
}
