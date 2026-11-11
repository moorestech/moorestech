using Client.Ui.Foundation.Text;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表タブ中身(読込中/不可/区画の1つ)
    /// Timetable tab content: one of loading, unavailable, section
    /// </summary>
    public sealed class TrainTimetableAreaView : VisualElement
    {
        private readonly TrainTimetableEditCommands _commands;
        private readonly LocalizedLabel _loading;
        private readonly LocalizedLabel _unavailable;
        private TrainTimetableSectionView _section;
        private ReadyTrainTimetableArea _sectionArea;

        public TrainTimetableAreaView(TrainTimetableEditCommands commands)
        {
            name = "train-timetable-area";
            _commands = commands;
            _loading = Message("train-timetable-loading", LocalizationKeys.Ui.BlockInventory.TimetableLoading);
            _unavailable = Message("train-timetable-unavailable", LocalizationKeys.Ui.BlockInventory.TimetableUnavailable);
            Add(_loading);
            Add(_unavailable);
            Show(_loading);
        }

        public void Render(TrainTimetableAreaViewModel model, string unnamedStationName)
        {
            if (model is not ReadyTrainTimetableArea ready)
            {
                RemoveSection();
                Show(model is LoadingTrainTimetableArea ? _loading : _unavailable);
                return;
            }

            // 列車が替わるたびに新しい区画を作る（Web の key=trainUnitId と同じく中の状態を持ち越さない）
            // A new section per train, carrying no inner state over (same as the Web key=trainUnitId)
            if (_section == null || !_sectionArea.IsSameTrain(ready))
            {
                RemoveSection();
                _section = new TrainTimetableSectionView(_commands);
                Add(_section);
            }
            _sectionArea = ready;
            Show(null);
            _section.Render(ready.Section, unnamedStationName);

            #region Internal

            void RemoveSection()
            {
                _section?.RemoveFromHierarchy();
                _section = null;
            }

            #endregion
        }

        private void Show(VisualElement message)
        {
            _loading.style.display = message == _loading ? DisplayStyle.Flex : DisplayStyle.None;
            _unavailable.style.display = message == _unavailable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static LocalizedLabel Message(string testid, LocalizationKey key)
        {
            var label = new LocalizedLabel { name = testid };
            label.SetKey(key);
            label.AddToClassList(UiTypographyClass.TextSm);
            return label;
        }
    }
}
