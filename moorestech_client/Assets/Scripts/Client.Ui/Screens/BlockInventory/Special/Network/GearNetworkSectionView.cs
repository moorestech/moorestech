using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 歯車網の集約と停止理由の区画（Web GearNetworkSection）
    /// Gear network aggregate and stop reason section (Web GearNetworkSection)
    /// </summary>
    public sealed class GearNetworkSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LackHighlightText _summary;
        private readonly LocalizedLabel _stopReason;

        public GearNetworkSectionView()
        {
            name = "gear-network-section";
            AddToClassList("special-section");
            _summary = new LackHighlightText("gear-network-summary", LackHighlightTextSize.Xs);
            SpecialViewParts.SeedEmptyText(_summary);
            _stopReason = SpecialViewParts.Label("gear-stop-reason", UiTypographyClass.TextXs, UiTypographyClass.TextInsufficient);
            _stack.AddItem(_summary);
            _stack.AddItem(_stopReason);
            Add(_stack);
        }

        public bool Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 詳細が無ければ描かずに偽を返し、呼び手が区画を隠す
            // Without the detail draw nothing and return false so the caller hides the section
            if (!detail.TryGetGearNetwork(out var network)) return false;

            // 止まっていれば理由の文を不足色で足す
            // Add the reason sentence in the shortage color when stopped
            _summary.Render(SpecialDetailTexts.GearNetworkSummary(network), false);
            var stopped = BlockDetailStateDisplay.TryGetStopReasonKey(network.StopReason, out var reasonKey);
            _stack.SetItemShown(_stopReason, stopped);
            if (stopped) _stopReason.SetText(new UiText(reasonKey));
            return true;
        }
    }
}
