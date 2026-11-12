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

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // Presenter は SpecialDetailVisibility が真（詳細あり）のときだけ呼ぶので、無いのは表と View の結線の誤り。隠れたまま古い値を残さず知らせる
            // The Presenter calls only when SpecialDetailVisibility is true (detail present), so a missing detail is a table/View wiring mistake; report it instead of leaving stale values
            if (!detail.TryGetGearNetwork(out var network))
            {
                UnityEngine.Debug.LogError("[GearNetworkSectionView] Render called without gear-network detail; SpecialDetailVisibility and the view disagree");
                return;
            }

            // 止まっていれば理由の文を不足色で足す
            // Add the reason sentence in the shortage color when stopped
            _summary.Render(SpecialDetailTexts.GearNetworkSummary(network), false);
            var stopped = BlockDetailStateDisplay.TryGetStopReasonKey(network.StopReason, out var reasonKey);
            _stack.SetItemShown(_stopReason, stopped);
            if (stopped) _stopReason.SetText(new UiText(reasonKey));
        }
    }
}
