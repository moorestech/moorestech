using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    // 時刻表タブが選ばれたとき、開いている列車の時刻表取得を始める
    // Start fetching the open train's timetable when the timetable tab is selected
    public class TrainTimetableOpenActionHandler : IActionHandler
    {
        public string ActionType => "train_timetable.open";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainTimetableFetcher _fetcher;

        public TrainTimetableOpenActionHandler(SubInventoryState subInventoryState, TrainUnitClientCache cache, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
            _fetcher = fetcher;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId))
            {
                // 車両未着でもタブを開いた事実は残す
                // Keep the tab-open fact even before the car arrives so a late car snapshot can start the fetch
                _fetcher.MarkTabOpenedWithoutTrain();
                return UniTask.FromResult(TrainTimetableActionSupport.Reject("train_not_open"));
            }
            _fetcher.RequestForOpenedTab(trainUnitId);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
