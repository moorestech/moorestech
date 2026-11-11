using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 電柱の本文。電力網の区画だけを包む（Web ElectricPoleInventory）
    /// Electric pole body wrapping only the electric network section (Web ElectricPoleInventory)
    /// </summary>
    public sealed class ElectricPoleBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly ElectricNetworkSectionView _network = new();

        public ElectricPoleBodyView()
        {
            name = "electric-pole-view";
            AddToClassList("electric-pole-view");
            Add(_network);
        }

        public bool Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 区画は自分の display を触らないので、包む本文が詳細の有無で出し入れする（Web の ElectricNetworkSection の return null）
            // Sections never touch their own display, so the wrapping body toggles it by detail presence (Web ElectricNetworkSection returns null)
            SpecialViewParts.SetShown(_network, _network.Render(itemSlots, detail));
            return true;
        }
    }
}
