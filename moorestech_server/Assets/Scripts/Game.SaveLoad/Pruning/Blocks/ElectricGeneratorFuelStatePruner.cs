using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning.Blocks
{
    // 電気発電機の保存形式を扱う。汎用のアイテム除去器へ燃焼状態の知識を入れない
    // Own the electric generator save format without teaching the generic item cleaner about combustion
    internal static class ElectricGeneratorFuelStatePruner
    {
        private const string SaveKey = "Game.Block.Blocks.PowerGenerator.VanillaElectricGeneratorComponent";

        internal static void StopWhenFuelRemoved(JProperty state, IEnumerable<JToken> removedItems)
        {
            // silent-ok: 別コンポーネントの同名フィールドには燃焼状態を補わない / 通常の除去記録と通知を維持する
            if (state.Name != SaveKey || state.Value is not JObject fuel) return;
            var removedFuel = removedItems.Any(item => (string)item["origin"]?["field"] == "currentFuelItemGuid"
                && string.IsNullOrEmpty((string)item["origin"]?["path"]));
            // silent-ok: 除去していない不整合はここで修復しない / 厳密な燃料復元検証がロード失敗を通知する
            if (!removedFuel) return;

            // GUIDだけ失った燃焼状態を残さず、両燃料と時間を一緒に止める
            // Stop both identities and the timer together instead of leaving burning state without its GUID
            fuel["activeFuelType"] = "None";
            fuel["currentFuelItemGuid"] = JValue.CreateNull();
            fuel["currentFuelFluidGuid"] = JValue.CreateNull();
            fuel["remainingFuelSeconds"] = 0d;
        }
    }
}
