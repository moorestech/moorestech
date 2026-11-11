using Client.Ui.Components.Controls;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの状態区画: 状態文（不足トーン）・プール量・接続済みなら昇降の進み、未接続なら接続ボタン（接続可能だけ押せる）（Web SpaceElevatorSection）
    /// Space elevator state section: status with shortage tone, pool amount, climb progress when connected, else a connect button enabled only when ready (Web SpaceElevatorSection)
    /// </summary>
    public sealed class SpaceElevatorSectionView : VisualElement
    {
        public const string CommandName = "space_elevator.connect";

        private readonly LocalizedLabel _loading = SpecialViewParts.Label("space-elevator-loading", UiTypographyClass.TextSm, UiTypographyClass.TextMuted);
        private readonly UiStack _section = new(UiGap.Px4) { name = "space-elevator-section" };
        private readonly LackHighlightText _status = new("space-elevator-status", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _pool = SpecialViewParts.Label("space-elevator-pool", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly GaugeBar _progress = new("space-elevator-progress");
        private readonly PanelActionButton _connect = new("space-elevator-connect", LocalizationKeys.Ui.BlockInventory.SpaceElevatorConnect);

        public SpaceElevatorSectionView(SpecialViewKit kit)
        {
            _loading.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading));
            SpecialViewParts.SeedEmptyText(_status);
            _connect.OnClicked.Subscribe(_ => kit.Run(CommandName, kit.Commands.ConnectSpaceElevator()));
            _section.AddItem(_status);
            _section.AddItem(_pool);
            _section.AddItem(_progress);
            _section.AddItem(_connect);
            Add(_loading);
            Add(_section);
        }

        public void Render(SpecialBlockDetailViewModel detail)
        {
            // 詳細の未着は取得待ちだけを出し、接続の操作を出さない
            // Pending detail shows only the loading line, without the connect operation
            var present = detail.TryGetSpaceElevator(out var elevator);
            SpecialViewParts.SetShown(_loading, !present);
            SpecialViewParts.SetShown(_section, present);
            if (!present) return;

            var display = elevator.StatusDisplay;
            _status.Render(new UiText(display.LabelKey), display.Insufficient);
            _pool.SetText(SpecialDetailTexts.SpaceElevatorPool(elevator));
            _section.SetItemShown(_progress, elevator.Connected);
            _section.SetItemShown(_connect, !elevator.Connected);
            _progress.SetValue(elevator.ClimbProgress);
            _connect.SetEnabled(elevator.CanConnect);
        }
    }
}
