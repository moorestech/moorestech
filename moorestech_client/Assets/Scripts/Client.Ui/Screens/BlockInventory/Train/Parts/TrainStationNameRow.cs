using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Platform.Edit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 駅名の行（Web TrainStationNameSection の写し）。入力と決定は編集のコマンドを直接呼ぶ（A-8）。送るのは決定だけ
    /// Station name row (copy of Web TrainStationNameSection); input and Set call the edit commands directly (A-8) and only Set sends
    /// </summary>
    public sealed class TrainStationNameRow : VisualElement
    {
        private readonly GameTextField _input;

        public TrainStationNameRow(ITrainPlatformEditCommands commands)
        {
            name = "train-station-name-section";
            AddToClassList("train-station-name");
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.BlockInventory.StationNameLabel);
            label.AddToClassList(UiTypographyClass.TextSm);

            // 入力欄は自然な幅（Web は伸ばさない）。間は右の 2 つの子に付ける画面のクラスで取る
            // The field keeps its natural width (Web does not stretch it); gaps come from a screen class on the two right children
            _input = new GameTextField("train-station-name-input", false, GameTextFieldLook.GaugeInset);
            _input.SetPlaceholder(new UiText(LocalizationKeys.Ui.BlockInventory.StationNamePlaceholder));
            _input.AddToClassList("train-station-name__row-end");
            _input.AddToClassList("train-station-name__input");
            _input.OnValueChanged.Subscribe(commands.EditStationName);
            var apply = new PanelActionButton("train-station-name-apply", LocalizationKeys.Ui.BlockInventory.StationNameApply);
            apply.AddToClassList("train-station-name__row-end");
            apply.OnClicked.Subscribe(_ => commands.ApplyStationName());
            Add(label);
            Add(_input);
            Add(apply);
        }

        public void Render(string input)
        {
            // 入力中（IME の組成を含む）の値と同じなら触らない
            // Leave the field alone when it already holds the value, including during IME composition
            if (_input.Field.value != input) _input.SetValueWithoutNotify(input);
        }
    }
}
