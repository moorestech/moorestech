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
    /// 搬入停止欄: 止まった理由を待っている品の名前つきで赤字で出す（Web MachineInputStallSection）。取れていない間は赤くない取得状態の 1 行だけ
    /// Inflow-stall section: the stall reason in red naming the waiting item (Web MachineInputStallSection); a non-red fetch-state line while unavailable
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
                _unavailable.Render(unavailable.FetchState);
                ContextRowList.MountOnly(this, _unavailable);
                return;
            }
            var stall = ((FetchedContextEntry<MachineInputStall>)entry).Value;
            var itemName = _content.ItemName(stall.WaitingItemId).Resolve();
            _stall.Render(new UiText(MachineInputStallLogic.MessageKey(stall.Reason), new[] { new LocalizationTextParam("itemName", itemName) }), true);
            ContextRowList.MountOnly(this, _stall);
        }
    }
}
