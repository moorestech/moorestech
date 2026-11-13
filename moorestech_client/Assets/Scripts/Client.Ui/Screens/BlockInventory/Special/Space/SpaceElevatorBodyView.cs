using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの本文: 入口・出口の群（サーバーの絶対添字・ホストの並びのまま）、状態区画、電力網の区画（Web SpaceElevatorInventory）
    /// Space elevator body: inlet and outlet groups (server absolute indices in host order), state section, electric network section (Web SpaceElevatorInventory)
    /// </summary>
    public sealed class SpaceElevatorBodyView : VisualElement, ISpecialSectionRenderer<SpecialBlockDetailViewModel>
    {
        private readonly UiStack _grid = new(UiGap.Sm) { name = "space-elevator-grid" };
        private readonly BlockItemGridGroup _inputGroup;
        private readonly BlockItemGridGroup _outputGroup;
        private readonly BlockItemGrid _input;
        private readonly BlockItemGrid _output;
        private readonly SpaceElevatorSectionView _section;
        private readonly ElectricNetworkSectionView _network = new();
        private readonly ElectricNetworkDetailSelector _networkSelector = new();
        private readonly UiStack _stack = new(UiGap.Sm);

        public SpaceElevatorBodyView(SpecialViewKit kit)
        {
            name = "space-elevator-view";
            AddToClassList("space-elevator-view");
            _input = kit.Grids.Create("space-elevator-input-grid");
            _output = kit.Grids.Create("space-elevator-output-grid");
            _inputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorSatelliteInput, _input);
            _outputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorResultOutput, _output);
            _grid.AddItem(_inputGroup);
            _grid.AddItem(_outputGroup);
            _section = new SpaceElevatorSectionView(kit);

            _stack.AddItem(_grid);
            _stack.AddItem(_section);
            _stack.AddItem(_network);
            Add(_stack);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 群は詳細が届いてから出す（Web は詳細無しで群を空にする）
            // Groups appear once the detail arrives (the Web leaves them empty without detail)
            var present = detail.TryGetSpaceElevator(out var elevator);
            _grid.SetItemShown(_inputGroup, present);
            _grid.SetItemShown(_outputGroup, present);
            if (present)
            {
                _input.Render(itemSlots, elevator.SatelliteInputSlots);
                _output.Render(itemSlots, elevator.OutputSlots);
            }
            _section.Render(detail);

            // 電力網の区画は Stack の項目なので selector で決めて SetItemShown で出し入れする（直に display を触ると先頭の間隔がずれる。D-23）
            // The network section is a Stack item, so decide with the selector and toggle via SetItemShown (touching display directly skews the leading gap. D-23)
            var showsNetwork = _networkSelector.TryGet(detail, out var network);
            if (showsNetwork) _network.Render(itemSlots, network);
            _stack.SetItemShown(_network, showsNetwork);
        }
    }
}
