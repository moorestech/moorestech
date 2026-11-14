using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 追加先の駅一覧の枠（Web TrainTimetableSection の駅一覧の写し）。追加は編集のコマンドを直接呼ぶ（A-8）
    /// Frame listing the stations that can be added (copy of the Web TrainTimetableSection station list); adds call the edit commands directly (A-8)
    /// </summary>
    public sealed class TrainTimetableStationListView : VisualElement
    {
        private readonly TrainTimetableEditCommands _commands;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;

        public TrainTimetableStationListView(TrainTimetableEditCommands commands)
        {
            _commands = commands;
            var panel = new GamePanel("train-timetable-stations-panel", GamePanelFrame.Craft);
            _list = new VisualElement { name = "train-timetable-stations" };
            _list.AddToClassList("train-timetable__list");
            _empty = new LocalizedLabel();
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStationsEmpty);
            _empty.AddToClassList(UiTypographyClass.TextSm);
            panel.Add(TrainTimetableHeading.Create(LocalizationKeys.Ui.BlockInventory.TimetableStations));
            panel.Add(new FadeRule());
            panel.Add(_list);
            Add(panel);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 0 件は列の中に文言を置く（Web と同じく testid は列に付く）
            // With no stations the text sits inside the list (the testid stays on the list, as in Web)
            _list.Clear();
            if (model.Stations.Count == 0)
            {
                _list.Add(_empty);
                return;
            }
            for (var i = 0; i < model.Stations.Count; i++) _list.Add(CreateRow(model.Stations[i], 0 < i, model.IsPending, unnamedStationName));
        }

        private VisualElement CreateRow(TrainTimetableStation station, bool following, bool disabled, string unnamedStationName)
        {
            // testid は座標を _ でつなぐ（負号はそのまま。例 -8_0_3）
            // The testid joins coordinates with _ and keeps minus signs (e.g. -8_0_3)
            var key = $"{station.Position.x}_{station.Position.y}_{station.Position.z}";
            var row = new VisualElement();
            row.AddToClassList("train-timetable__row");
            row.EnableInClassList("train-timetable__row--following", following);
            var label = new LocalizedLabel();
            label.SetText(TimetableEditLogic.StationLabel(station, unnamedStationName));
            label.AddToClassList("train-timetable__label");

            // 押下は行の駅のまま編集のコマンドへ。行を作り直すと古いボタンごと購読も捨てられる
            // Presses go to the edit commands with the row's station; rebuilding rows discards old buttons with their subscriptions
            var add = new PanelActionButton($"train-timetable-station-{key}-add", new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAdd));
            add.AddToClassList("train-timetable__control");
            add.AddToClassList("train-timetable__row-end");
            add.SetEnabled(!disabled);
            add.OnClicked.Subscribe(_ => _commands.Add(station));
            row.Add(label);
            row.Add(add);
            return row;
        }
    }
}
