using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 電柱の本文。電力網の区画だけを包む（Web ElectricPoleInventory）
    /// Electric pole body wrapping only the electric network section (Web ElectricPoleInventory)
    /// </summary>
    public sealed class ElectricPoleBodyView : VisualElement, ISpecialSectionRenderer<SpecialBlockDetailViewModel>
    {
        private readonly ElectricNetworkSectionView _network = new();
        private readonly ElectricNetworkDetailSelector _networkSelector = new();

        public ElectricPoleBodyView()
        {
            name = "electric-pole-view";
            AddToClassList("electric-pole-view");
            Add(_network);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 区画は自分の display を触らないので、包む本文が selector で決めて出し入れする（Web の ElectricNetworkSection の return null）
            // Sections never touch their own display, so the wrapping body decides with the selector and toggles it (Web ElectricNetworkSection returns null)
            var shows = _networkSelector.TryGet(detail, out var network);
            if (shows) _network.Render(itemSlots, network);
            UiDisplay.SetShown(_network, shows);
        }
    }
}
