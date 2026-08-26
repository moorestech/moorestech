using Client.Game.InGame.UI.Inventory.Equipment;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// inventory.select_equipment: 装備の選択スロットを設定する
    /// inventory.select_equipment: set the selected equipment slot
    /// </summary>
    public class SelectEquipmentActionHandler : IActionHandler
    {
        public string ActionType => "inventory.select_equipment";

        private readonly LocalPlayerEquipment _equipment;

        public SelectEquipmentActionHandler(LocalPlayerEquipment equipment)
        {
            _equipment = equipment;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // index は int 範囲の整数のみ許可する
            // index must be an int-range integer
            if (payload["index"] is not JValue { Value: long indexLong } || indexLong < int.MinValue || int.MaxValue < indexLong)
                return UniTask.FromResult(ActionResult.Fail("invalid_index"));

            // 範囲丸めは LocalPlayerEquipment 側で行うためそのまま渡す
            // LocalPlayerEquipment clamps to range, so pass the value straight through
            _equipment.SetSelectedIndex((int)indexLong);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
