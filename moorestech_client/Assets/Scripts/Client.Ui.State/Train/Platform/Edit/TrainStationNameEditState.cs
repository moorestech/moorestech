using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書きの器。パネルごとに下書きを作り直し、同期した名前を下書きへ流す
    /// Holder of the station name draft; rebuilds the draft per panel and feeds synced names into it
    /// </summary>
    public sealed class TrainStationNameEditState : IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ITrainPlatformBodySource _bodies;
        private readonly ReactiveProperty<string> _input = new(string.Empty);
        private BlockInventoryPanelState _panel = BlockInventoryPanelState.Closed;

        public IReadOnlyReactiveProperty<string> Input => _input;
        public TrainStationNameDraft Draft { get; private set; } = new();

        public TrainStationNameEditState(IBlockInventoryPanelSource panels, ITrainPlatformBodySource bodies)
        {
            _panels = panels;
            _bodies = bodies;
        }

        public void Initialize()
        {
            _panels.Current.Subscribe(ReceivePanel);
            _bodies.Current.Subscribe(_ => SyncDraft());
        }

        public void Edit(string input)
        {
            Draft.Edit(input);
            _input.Value = Draft.Input;
        }

        // 下書きは閉じる・別のパネルのたびに作り直すので、開閉の世代も兼ねる。送った回の下書きが今のものでなければ戻さない
        // Drafts are rebuilt on every close or panel change, so they double as the open generation; revert only the draft that sent
        public void RevertIfCurrent(TrainStationNameDraft draftAtSend)
        {
            if (!ReferenceEquals(Draft, draftAtSend))
            {
                Debug.LogWarning("[TrainStationName] set_name refused after the draft was replaced; nothing to revert");
                return;
            }
            Draft.Reject();
            _input.Value = Draft.Input;
        }

        private void ReceivePanel(BlockInventoryPanelState panel)
        {
            // silent-ok: 同じパネルの再配信では下書きを保つ / 打ちかけの入力を消さないため。別のパネルは下で作り直す
            if (panel.IsSamePanel(_panel)) return;
            _panel = panel;
            Draft = new TrainStationNameDraft();
            SyncDraft();
        }

        private void SyncDraft()
        {
            if (_bodies.Current.Value is OpenTrainPlatformBody { StationName: { IsPresent: true } stationName }) Draft.Sync(stationName.Value.SyncedName);
            _input.Value = Draft.Input;
        }
    }
}
