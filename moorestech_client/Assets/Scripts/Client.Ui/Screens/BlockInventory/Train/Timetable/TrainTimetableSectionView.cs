using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表の区画（Web TrainTimetableSection の写し）。自動運転の行・停車駅・駅一覧
    /// Timetable section (copy of Web TrainTimetableSection): auto-run row, stops and stations
    /// </summary>
    public sealed class TrainTimetableSectionView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Xs);
        private readonly ModeSwitch<bool> _autoRun;
        private readonly LocalizedLabel _autoRunUnavailable;
        private readonly TrainTimetableStopListView _stops;
        private readonly TrainTimetableStationListView _stations;

        public TrainTimetableSectionView(TrainTimetableEditCommands commands)
        {
            name = "train-timetable-section";
            AddToClassList("train-timetable");

            // 自動運転の行: 左に見出し・右に ON/OFF。押下は編集のコマンドを直接呼ぶ（A-8）
            // Auto-run row: heading left, ON/OFF right; presses call the edit commands directly (A-8)
            var autoRunRow = new VisualElement();
            autoRunRow.AddToClassList("train-timetable__row");
            var autoRunLabel = new LocalizedLabel();
            autoRunLabel.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRun);
            autoRunLabel.AddToClassList(UiTypographyClass.TextSm);
            _autoRun = new ModeSwitch<bool>("train-timetable-auto-run", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<bool>("train-timetable-auto-run-on", true, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOn)),
                new ModeSwitchOption<bool>("train-timetable-auto-run-off", false, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOff)),
            });
            _autoRun.AddToClassList("train-timetable__row-end");
            _autoRun.OnOptionSelected.Subscribe(enabled => commands.SetAutoRun(enabled));
            autoRunRow.Add(autoRunLabel);
            autoRunRow.Add(_autoRun);
            _autoRunUnavailable = new LocalizedLabel { name = "train-timetable-auto-run-unavailable" };
            _autoRunUnavailable.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunUnavailable);
            _autoRunUnavailable.AddToClassList(UiTypographyClass.TextSm);

            _stops = new TrainTimetableStopListView(commands);
            _stations = new TrainTimetableStationListView(commands);
            _stack.AddItem(autoRunRow);
            _stack.AddItem(_autoRunUnavailable);
            _stack.AddItem(_stops);
            _stack.AddItem(_stations);
            Add(_stack);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 保留中は全操作封鎖、停車駅0件はONも封鎖
            // Pending blocks all; ON also blocked with no stops
            _autoRun.SetSelected(model.IsAutoRun);
            _autoRun.SetOptionEnabled(true, !model.IsPending && model.CanEnableAutoRun);
            _autoRun.SetOptionEnabled(false, !model.IsPending);
            _stack.SetItemShown(_autoRunUnavailable, !model.CanEnableAutoRun);
            _stops.Render(model, unnamedStationName);
            _stations.Render(model, unnamedStationName);
        }
    }
}
