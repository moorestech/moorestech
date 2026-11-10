using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// source=train の本文を作る（alpha の拡張口）。Presenter と View の組を返す
    /// Creates the source=train body for alpha's extension point, returning a Presenter and View pair
    /// </summary>
    public sealed class TrainInventoryBodyFactory : ITrainInventoryBodyFactory
    {
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableEditModel _timetable;
        private readonly ITrainTimetableEditCommands _timetableEdit;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly ITrainTimetableCommands _timetableCommands;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public TrainInventoryBodyFactory(ITrainInventoryBodySource body, ITrainTimetableEditModel timetable, ITrainTimetableEditCommands timetableEdit, ITrainTimetableSource timetableSource, ITrainTimetableCommands timetableCommands, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _body = body;
            _timetable = timetable;
            _timetableEdit = timetableEdit;
            _timetableSource = timetableSource;
            _timetableCommands = timetableCommands;
            _gridFactory = gridFactory;
            _content = content;
        }

        public ITrainInventoryPart Create()
        {
            var contents = new TrainInventoryContentsView(_gridFactory.Create("train-inventory-slots"), new FluidInventoryContents("train-inventory-fluids", _content));
            var view = new TrainInventoryBodyView(contents, new TrainTimetableAreaView(_timetableEdit));
            return new TrainInventoryBodyPresenter(view, _body, _timetable, _timetableSource, _timetableCommands);
        }
    }
}
