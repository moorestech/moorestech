using System;
using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Cargo;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの本文: 一覧のモードでは積荷の一覧、そうでなければ見出し・入口・出口・状態区画・電力網（ADR 0106 の 7）。モードは Presenter が決める（未選択は接続の有無にかかわらず一覧）
    /// Space elevator body: the cargo list in list mode, otherwise headline, inlet, outlets, state section and electric network (ADR 0106 §7); the presenter decides the mode (unselected shows the list regardless of connection)
    /// </summary>
    public sealed class SpaceElevatorBodyView : VisualElement, IOrbitalCargoBodyView
    {
        private const string InputGridTestId = "space-elevator-input-grid";

        private readonly UiStack _grid = new(UiGap.Sm) { name = "space-elevator-grid" };
        private readonly BlockItemGridGroup _inputGroup;
        private readonly BlockItemGridGroup _outputGroup;
        private readonly BlockItemGrid _input;
        private readonly BlockItemGrid _output;
        private readonly SpaceElevatorSectionView _section;
        private readonly ElectricNetworkSectionView _network = new();
        private readonly ElectricNetworkDetailSelector _networkSelector = new();
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly OrbitalCargoPane _pane;
        private readonly GameContentPresentation _content;

        public IObservable<ItemId> OnCargoRowPressed => _pane.OnRowPressed;
        public IObservable<Unit> OnChangeCargoRequested => _pane.OnChangeCargoRequested;

        public SpaceElevatorBodyView(SpecialViewKit kit, bool fillsPanelHeight)
        {
            name = "space-elevator-view";
            AddToClassList("space-elevator-view");
            AddToClassList("orbital-cargo-host");
            pickingMode = PickingMode.Ignore;
            _content = kit.Presentation;
            _input = kit.Grids.Create(InputGridTestId);
            _output = kit.Grids.Create("space-elevator-output-grid");
            _inputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorSatelliteInput, _input);
            _outputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorResultOutput, _output);
            _grid.AddItem(_inputGroup);
            _grid.AddItem(_outputGroup);
            _section = new SpaceElevatorSectionView(kit);

            _stack.AddItem(_grid);
            _stack.AddItem(_section);
            _stack.AddItem(_network);
            _pane = new OrbitalCargoPane("space-elevator", kit, _stack, fillsPanelHeight);
            Add(_pane);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail, bool showsSelection)
        {
            // 群は詳細が届いてから出す。未着の間は中身だけを出し、状態区画が取得待ちを語る
            // Groups appear once the detail arrives; while pending only the content shows and the state section shows loading
            var present = detail.TryGetSpaceElevator(out var elevator);
            _grid.SetItemShown(_inputGroup, present);
            _grid.SetItemShown(_outputGroup, present);
            if (present)
            {
                OrbitalCargoSlotContents.Render(_input, InputGridTestId, itemSlots, elevator.InletBoundSlots, _content);
                _output.Render(itemSlots, elevator.OutputSlots);
                _pane.Render(elevator.Cargo, new UiText(elevator.StatusDisplay.LabelKey), showsSelection);
            }
            else
            {
                _pane.RenderWithoutCargo();
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
