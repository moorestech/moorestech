using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文の Presenter。本文の状態と駅名の入力を購読して View へ流すだけ（下書き・送信は State。D-33）
    /// Presenter of the station and platform body: only subscribes the body state and the name input into the View (draft and sends stay in State; D-33)
    /// </summary>
    public sealed class TrainPlatformBodyPresenter : IBlockInventoryPart
    {
        private readonly TrainPlatformBodyView _view;
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainPlatformEditModel _edit;
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;

        public TrainPlatformBodyPresenter(TrainPlatformBodyView view, ITrainPlatformBodySource source, ITrainPlatformEditModel edit)
        {
            _view = view;
            _source = source;
            _edit = edit;
        }

        // ブロックの識別は Model がパネルの源から読むので、ここでは使わない
        // Models read the block identity from the panel source, so it is unused here
        public void Attach(OpenBlockInventoryPanel panel)
        {
            _source.Current.Subscribe(_ => Render()).AddTo(_subscriptions);
            _edit.StationNameInput.Subscribe(_ => Render()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Render()
        {
            _view.Render(_source.Current.Value, _edit.StationNameInput.Value);
        }
    }
}
