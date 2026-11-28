using System;
using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Cargo;
using Client.Ui.Screens.BlockInventory.Special.Registration;
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
    /// 発射台の本文: 一覧のモードでは積荷の一覧、そうでなければ見出し・資材・選んだ積荷・状態・進捗・出力（ADR 0106 の 1）。モードは Presenter が決める。詳細の未着は取得待ち
    /// Launch pad body: the cargo list in list mode, otherwise headline, materials, selected payload, status, progress and outputs (ADR 0106 §1); the presenter decides the mode; pending detail shows loading
    /// </summary>
    public sealed class RocketLaunchPadBodyView : VisualElement, IOrbitalCargoBodyView
    {
        private const string MaterialGridTestId = "rocket-launch-pad-material-slots";
        private const string PayloadGridTestId = "rocket-launch-pad-payload-slots";

        private static readonly UiText LoadingText = new(LocalizationKeys.Ui.BlockInventory.DetailLoading);
        private readonly LocalizedLabel _loading = SpecialViewParts.Label("rocket-launch-pad-loading", UiTypographyClass.TextSm, UiTypographyClass.TextMuted);
        private readonly UiStack _view = new(UiGap.Sm) { name = "rocket-launch-pad-view" };
        private readonly BlockItemGrid _material;
        private readonly BlockItemGrid _payload;
        private readonly BlockItemGrid _output;
        private readonly LocalizedLabel _status = SpecialViewParts.Label("rocket-launch-pad-status", UiTypographyClass.TextSm, UiTypographyClass.TextHighContrast);
        private readonly GaugeBar _progress = new("rocket-launch-pad-progress");
        private readonly OrbitalCargoPane _pane;
        private readonly GameContentPresentation _content;

        public IObservable<ItemId> OnCargoRowPressed => _pane.OnRowPressed;
        public IObservable<Unit> OnChangeCargoRequested => _pane.OnChangeCargoRequested;

        public RocketLaunchPadBodyView(SpecialViewKit kit, bool fillsPanelHeight)
        {
            AddToClassList("rocket-launch-pad-view");
            AddToClassList("orbital-cargo-host");
            pickingMode = PickingMode.Ignore;
            _content = kit.Presentation;
            _loading.SetText(LoadingText);
            _material = kit.Grids.Create(MaterialGridTestId);
            _payload = kit.Grids.Create(PayloadGridTestId);
            _output = kit.Grids.Create("rocket-launch-pad-output-slots");

            // 選んだ後の並び: 資材・積荷・状態・進捗・出力（見出しは一覧と出し分ける枠が上に置く）
            // Order after selecting: materials, payload, status, progress, outputs (the pane puts the headline above)
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketMaterials, _material));
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketPayloads, _payload));
            _view.AddItem(_status);
            _view.AddItem(_progress);
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketOutputs, _output));
            _pane = new OrbitalCargoPane("rocket-launch-pad", kit, _view, fillsPanelHeight);
            Add(_loading);
            Add(_pane);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail, bool showsSelection)
        {
            var present = detail.TryGetRocketLaunchPad(out var rocket);
            _loading.SetText(detail.Unavailable ?? LoadingText);
            UiDisplay.SetShown(_loading, !present);
            UiDisplay.SetShown(_pane, present);
            // silent-ok: 詳細の未着は「状態の取得を待っています」を出したまま / 録画（R-12）の発射台の段で気づく
            if (!present) return;

            // 資材と選んだ積荷は透かしと「持っている数／必要な数」で描く（ADR 0106 の 1）
            // Materials and the selected payload draw ghosts and held/required (ADR 0106 §1)
            OrbitalCargoSlotContents.Render(_material, MaterialGridTestId, itemSlots, rocket.MaterialBoundSlots, _content);
            OrbitalCargoSlotContents.Render(_payload, PayloadGridTestId, itemSlots, rocket.PayloadBoundSlots(itemSlots), _content);
            _output.Render(itemSlots, rocket.OutputSlots(itemSlots.Count));
            var status = new UiText(rocket.StatusKey);
            _status.SetText(status);
            _progress.SetValue(rocket.Progress);
            _pane.Render(rocket.Cargo, status, showsSelection);
        }
    }
}
