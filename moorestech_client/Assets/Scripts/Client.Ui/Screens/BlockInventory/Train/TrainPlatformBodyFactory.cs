using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// TrainStation・TrainItemPlatform・TrainFluidPlatform の本文を作る（Kind TrainPlatform）
    /// Creates the body for TrainStation, TrainItemPlatform and TrainFluidPlatform (Kind TrainPlatform)
    /// </summary>
    public sealed class TrainPlatformBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainPlatformEditModel _edit;
        private readonly ITrainPlatformEditCommands _commands;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.TrainPlatform;

        public TrainPlatformBodyFactory(ITrainPlatformBodySource source, ITrainPlatformEditModel edit, ITrainPlatformEditCommands commands, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _source = source;
            _edit = edit;
            _commands = commands;
            _gridFactory = gridFactory;
            _content = content;
        }

        public IBlockInventoryPart Create(BlockInventoryBodyMount mount)
        {
            var view = new TrainPlatformBodyView(_gridFactory.Create("train-platform-item-slots"), new FluidInventoryContents("train-platform-fluid-slots", _content), _commands);
            return new TrainPlatformBodyPresenter(view, _source, _edit);
        }
    }
}
