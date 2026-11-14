using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
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
    /// 停車駅の枠（Web TrainTimetableStopList の写し）。押下は編集のコマンドを直接呼ぶ（A-8）
    /// Stop list frame (copy of Web TrainTimetableStopList); presses call the edit commands directly (A-8)
    /// </summary>
    public sealed class TrainTimetableStopListView : VisualElement
    {
        private const string ListTestId = "train-timetable-stops";
        private readonly TrainTimetableEditCommands _commands;
        private readonly IUiOsCursor _osCursor;
        private readonly GamePanel _panel;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;

        public TrainTimetableStopListView(TrainTimetableEditCommands commands, IUiOsCursor osCursor)
        {
            _commands = commands;
            _osCursor = osCursor;

            // Craft 枠に見出し（内側余白は器、文字の型はラベル）・飾り線・列（空なら文言）
            // A craft frame with the heading (padding on the wrapper, type on the label), the fade rule and the list or empty text
            _panel = new GamePanel("train-timetable-stops-panel", GamePanelFrame.Craft);
            _empty = new LocalizedLabel { name = ListTestId };
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStopsEmpty);
            _empty.AddToClassList(UiTypographyClass.TextSm);
            _list = new VisualElement { name = ListTestId };
            _list.AddToClassList("train-timetable__list");
            _panel.Add(TrainTimetableHeading.Create(LocalizationKeys.Ui.BlockInventory.TimetableStops));
            _panel.Add(new FadeRule());
            Add(_panel);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 空の文言と列は同じ testid なので、片方だけを木に置く
            // The empty text and the list share a testid, so only one stays in the tree
            _empty.RemoveFromHierarchy();
            _list.RemoveFromHierarchy();
            if (model.Stops.Count == 0)
            {
                _panel.Add(_empty);
                return;
            }
            _list.Clear();
            for (var i = 0; i < model.Stops.Count; i++) _list.Add(CreateRow(model, i, unnamedStationName));
            _panel.Add(_list);
        }

        private VisualElement CreateRow(TrainTimetableSectionViewModel model, int index, string unnamedStationName)
        {
            var stop = model.Stops[index];
            var row = new VisualElement { name = $"train-timetable-stop-{index}" };
            row.AddToClassList("train-timetable__row");
            row.EnableInClassList("train-timetable__row--following", 0 < index);
            row.EnableInClassList("train-timetable-stop--current", model.CurrentIndex == index);

            // ラベルは「番号. 駅名(座標)」。駅名の空は言語で変わるので Presenter が言語の変化で描き直す（D-10）
            // The label is "index. station(coords)"; the Presenter re-renders on language change since the unnamed name localizes (D-10)
            var station = TimetableEditLogic.StationLabel(new TrainTimetableStation(stop.Position, stop.Name), unnamedStationName).Resolve();
            var label = new LocalizedLabel { name = $"train-timetable-stop-{index}-label" };
            label.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.TimetableStopLabel, new[]
            {
                new LocalizationTextParam("index", UiNumberFormat.Integer(index + 1)),
                new LocalizationTextParam("station", station),
            }));
            label.AddToClassList("train-timetable__label");

            // 押下は行の番号つきで編集のコマンドへ。行を作り直すと古いボタンごと購読も捨てられる
            // Presses go to the edit commands with the row index; rebuilding rows discards old buttons with their subscriptions
            var up = Control($"train-timetable-stop-{index}-up", LocalizationKeys.Ui.BlockInventory.TimetableMoveUp, model.IsPending || index == 0);
            var down = Control($"train-timetable-stop-{index}-down", LocalizationKeys.Ui.BlockInventory.TimetableMoveDown, model.IsPending || index == model.Stops.Count - 1);
            var remove = Control($"train-timetable-stop-{index}-remove", LocalizationKeys.Ui.BlockInventory.TimetableRemove, model.IsPending);
            up.OnClicked.Subscribe(_ => _commands.Move(index, StopMoveDirection.Up));
            down.OnClicked.Subscribe(_ => _commands.Move(index, StopMoveDirection.Down));
            remove.OnClicked.Subscribe(_ => _commands.Remove(index));

            // 2 個目以降の操作に間のクラス（UITK の USS に兄弟の結合子が無いので C# で付ける。前例 ModeSwitch の --following）
            // Gap class on the second and later controls (UITK USS has no sibling combinator; precedent ModeSwitch --following)
            down.AddToClassList("train-timetable__control--following");
            remove.AddToClassList("train-timetable__control--following");
            var controls = new VisualElement();
            controls.AddToClassList("train-timetable__controls");
            controls.AddToClassList("train-timetable__row-end");
            controls.Add(up);
            controls.Add(down);
            controls.Add(remove);
            row.Add(label);
            row.Add(controls);
            return row;
        }

        private PanelActionButton Control(string testid, LocalizationKey label, bool disabled)
        {
            var button = new PanelActionButton(testid, new UiText(label), _osCursor);

            button.AddToClassList("train-timetable__control");
            button.SetEnabled(!disabled);
            return button;
        }
    }
}
