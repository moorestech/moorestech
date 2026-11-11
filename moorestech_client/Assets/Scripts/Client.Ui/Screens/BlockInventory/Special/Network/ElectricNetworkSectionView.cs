using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 電力網の集約の区画。消費者 0 は「需要なし」、他は集約の 1 行（Web ElectricNetworkSection）
    /// Electric network aggregate section; zero consumers shows "no demand", otherwise one summary row (Web ElectricNetworkSection)
    /// </summary>
    public sealed class ElectricNetworkSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LocalizedLabel _noDemand;
        private readonly LackHighlightText _summary;

        public ElectricNetworkSectionView()
        {
            name = "electric-network-section";
            AddToClassList("special-section");
            _noDemand = SpecialViewParts.Label("electric-network-no-demand", UiTypographyClass.TextXs, UiTypographyClass.TextMuted);
            _noDemand.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.NoDemand));
            _summary = new LackHighlightText("electric-network-summary", LackHighlightTextSize.Xs);
            SpecialViewParts.SeedEmptyText(_summary);
            _stack.AddItem(_noDemand);
            _stack.AddItem(_summary);
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetElectricNetwork(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetElectricNetwork(out var network)) return;

            var noDemand = network.ConsumerCount == 0;
            _stack.SetItemShown(_noDemand, noDemand);
            _stack.SetItemShown(_summary, !noDemand);
            if (!noDemand) _summary.Render(SpecialDetailTexts.ElectricNetworkSummary(network), false);
        }
    }
}
