using Client.Game.InGame.UI.Inventory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// inventory.select_hotbar: ホットバーの選択スロットを設定する
    /// inventory.select_hotbar: set the selected hotbar slot
    /// </summary>
    public class SelectHotbarActionHandler : IActionHandler
    {
        public string ActionType => "inventory.select_hotbar";

        private readonly HotBarView _hotBarView;

        public SelectHotbarActionHandler(HotBarView hotBarView)
        {
            _hotBarView = hotBarView;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // index は int 範囲の整数のみ許可する
            // index must be an int-range integer
            if (payload["index"] is not JValue { Value: long indexLong } || indexLong < int.MinValue || int.MaxValue < indexLong)
                return UniTask.FromResult(ActionResult.Fail("invalid_index"));

            // 範囲丸めは HotBarView 側で行うためそのまま渡す
            // HotBarView clamps to range, so pass the value straight through
            _hotBarView.SetSelectIndex((int)indexLong);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
