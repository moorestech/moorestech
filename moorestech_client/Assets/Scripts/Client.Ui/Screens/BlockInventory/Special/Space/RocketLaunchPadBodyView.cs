using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 発射台の本文: 資材・積荷の群、状態文、進捗、出力の群（サーバーの絶対添字のまま）。詳細の未着は取得待ち（Web RocketLaunchPadInventory）
    /// Launch pad body: material and payload groups, status, progress, output group (server absolute indices); pending detail shows loading (Web RocketLaunchPadInventory)
    /// </summary>
    public sealed class RocketLaunchPadBodyView : VisualElement, ISpecialSectionRenderer<SpecialBlockDetailViewModel>
    {
        private readonly LocalizedLabel _loading = SpecialViewParts.Label("rocket-launch-pad-loading", UiTypographyClass.TextSm, UiTypographyClass.TextMuted);
        private readonly UiStack _view = new(UiGap.Sm) { name = "rocket-launch-pad-view" };
        private readonly BlockItemGrid _material;
        private readonly BlockItemGrid _payload;
        private readonly BlockItemGrid _output;
        private readonly LocalizedLabel _status = SpecialViewParts.Label("rocket-launch-pad-status", UiTypographyClass.TextSm, UiTypographyClass.TextHighContrast);
        private readonly GaugeBar _progress = new("rocket-launch-pad-progress");

        public RocketLaunchPadBodyView(SpecialViewKit kit)
        {
            AddToClassList("rocket-launch-pad-view");
            _loading.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading));
            _material = kit.Grids.Create("rocket-launch-pad-material-slots");
            _payload = kit.Grids.Create("rocket-launch-pad-payload-slots");
            _output = kit.Grids.Create("rocket-launch-pad-output-slots");

            // Web と同じ並び: 資材・積荷・状態・進捗・出力
            // Same order as the Web: materials, payloads, status, progress, outputs
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketMaterials, _material));
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketPayloads, _payload));
            _view.AddItem(_status);
            _view.AddItem(_progress);
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketOutputs, _output));
            Add(_loading);
            Add(_view);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            var present = detail.TryGetRocketLaunchPad(out var rocket);
            SpecialViewParts.SetShown(_loading, !present);
            SpecialViewParts.SetShown(_view, present);
            // silent-ok: 詳細の未着は「状態の取得を待っています」を出したまま / 録画（R-12）の発射台の段で気づく
            if (!present) return;

            _material.Render(itemSlots, rocket.MaterialSlots);
            _payload.Render(itemSlots, rocket.PayloadSlots);
            _output.Render(itemSlots, rocket.OutputSlots(itemSlots.Count));
            _status.SetText(new UiText(rocket.StatusKey));
            _progress.SetValue(rocket.Progress);
        }
    }
}
